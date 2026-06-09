import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { dashboards, type DashboardData } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import TopBar from "../components/TopBar";
import Chart from "../components/Chart";

export default function DashboardView() {
  const { id = "" } = useParams();
  const { state } = useAuth();
  const [data, setData] = useState<DashboardData | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      setData(await dashboards.data(id));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  useEffect(() => {
    void load();
  }, [id, state?.organizationId]);

  const removeWidget = async (widgetId: string) => {
    try {
      await dashboards.removeWidget(id, widgetId);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="page">
      <TopBar />
      <main>
        <h2>
          <Link to="/dashboards" className="nav">
            Dashboards
          </Link>{" "}
          / {data?.name}
        </h2>

        {error && <p className="error">{error}</p>}

        {data && data.widgets.length === 0 && (
          <p className="hint">
            No widgets yet. Open a dataset's Explore view and save a chart here.
          </p>
        )}

        <div className="widget-grid">
          {data?.widgets.map((w) => (
            <section key={w.id} className="panel widget">
              <div className="widget-head">
                <h3>{w.title}</h3>
                <button className="ghost danger" onClick={() => void removeWidget(w.id)}>
                  Remove
                </button>
              </div>
              <Chart type={w.chartType} points={w.points} />
              {w.insights.length > 0 && (
                <ul className="insights">
                  {w.insights.slice(0, 2).map((ins, i) => (
                    <li key={i}>{ins}</li>
                  ))}
                </ul>
              )}
            </section>
          ))}
        </div>
      </main>
    </div>
  );
}
