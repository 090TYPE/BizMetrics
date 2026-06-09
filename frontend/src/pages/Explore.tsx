import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import {
  analytics,
  dashboards,
  datasets,
  type Aggregation,
  type AnalyticsQuery,
  type ChartType,
  type Dataset,
  type DashboardSummary,
  type QueryResult,
  type TimeBucket,
} from "../api/client";
import { useAuth } from "../auth/AuthContext";
import TopBar from "../components/TopBar";
import Chart from "../components/Chart";

const AGGS: Aggregation[] = ["Count", "Sum", "Average", "Min", "Max"];
const BUCKETS: TimeBucket[] = ["None", "Day", "Week", "Month"];
const CHARTS: ChartType[] = ["bar", "line", "pie"];

export default function Explore() {
  const { id = "" } = useParams();
  const { state } = useAuth();

  const [dataset, setDataset] = useState<Dataset | null>(null);
  const [groupBy, setGroupBy] = useState("");
  const [measure, setMeasure] = useState("");
  const [agg, setAgg] = useState<Aggregation>("Count");
  const [bucket, setBucket] = useState<TimeBucket>("None");
  const [chartType, setChartType] = useState<ChartType>("bar");
  const [result, setResult] = useState<QueryResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  // Save-to-dashboard
  const [dashList, setDashList] = useState<DashboardSummary[]>([]);
  const [targetDash, setTargetDash] = useState("");
  const [title, setTitle] = useState("");
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    datasets.get(id).then((d) => {
      setDataset(d);
      setGroupBy(d.columns[0] ?? "");
      setMeasure(d.columns[1] ?? d.columns[0] ?? "");
      setTitle(d.name);
    });
    dashboards.list().then((l) => {
      setDashList(l);
      setTargetDash(l[0]?.id ?? "");
    });
  }, [id, state?.organizationId]);

  const buildQuery = (): AnalyticsQuery => ({
    groupBy: groupBy || null,
    bucket,
    measure: agg === "Count" ? null : measure || null,
    agg,
    topN: bucket === "None" ? 12 : null,
  });

  const run = async () => {
    setError(null);
    try {
      setResult(await analytics.query(id, buildQuery()));
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const save = async () => {
    if (!targetDash) {
      setError("Create a dashboard first (Dashboards tab).");
      return;
    }
    setNotice(null);
    try {
      await dashboards.addWidget(targetDash, {
        datasetId: id,
        title: title.trim() || "Untitled",
        chartType,
        query: buildQuery(),
      });
      setNotice("Saved to dashboard.");
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="page">
      <TopBar />
      <main>
        <h2>Explore — {dataset?.name}</h2>
        {dataset && dataset.status !== "Ready" && (
          <p className="hint">This dataset is {dataset.status}; explore once it's Ready.</p>
        )}

        <div className="controls">
          <label>
            Group by
            <select value={groupBy} onChange={(e) => setGroupBy(e.target.value)}>
              {dataset?.columns.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </label>
          <label>
            Time bucket
            <select value={bucket} onChange={(e) => setBucket(e.target.value as TimeBucket)}>
              {BUCKETS.map((b) => (
                <option key={b} value={b}>
                  {b}
                </option>
              ))}
            </select>
          </label>
          <label>
            Aggregation
            <select value={agg} onChange={(e) => setAgg(e.target.value as Aggregation)}>
              {AGGS.map((a) => (
                <option key={a} value={a}>
                  {a}
                </option>
              ))}
            </select>
          </label>
          <label>
            Measure
            <select
              value={measure}
              onChange={(e) => setMeasure(e.target.value)}
              disabled={agg === "Count"}
            >
              {dataset?.columns.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </label>
          <label>
            Chart
            <select value={chartType} onChange={(e) => setChartType(e.target.value as ChartType)}>
              {CHARTS.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </label>
          <button onClick={() => void run()}>Run</button>
        </div>

        {error && <p className="error">{error}</p>}

        {result && (
          <section className="panel">
            <h3>{result.label}</h3>
            <Chart type={chartType} points={result.points} />
            {result.insights.length > 0 && (
              <ul className="insights">
                {result.insights.map((ins, i) => (
                  <li key={i}>{ins}</li>
                ))}
              </ul>
            )}

            <div className="controls">
              <label>
                Save as
                <input value={title} onChange={(e) => setTitle(e.target.value)} />
              </label>
              <label>
                To dashboard
                <select value={targetDash} onChange={(e) => setTargetDash(e.target.value)}>
                  {dashList.length === 0 && <option value="">(none yet)</option>}
                  {dashList.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </label>
              <button onClick={() => void save()} disabled={!targetDash}>
                Save widget
              </button>
            </div>
            {notice && <p className="notice">{notice}</p>}
          </section>
        )}
      </main>
    </div>
  );
}
