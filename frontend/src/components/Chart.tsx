import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type { AnalyticsPoint, ChartType } from "../api/client";

const COLORS = ["#6366f1", "#34d399", "#fbbf24", "#f87171", "#60a5fa", "#c084fc", "#fb923c", "#2dd4bf"];

export default function Chart({ type, points }: { type: ChartType; points: AnalyticsPoint[] }) {
  if (points.length === 0) return <p className="hint">No data.</p>;

  const data = points.map((p) => ({ key: p.key, value: p.value }));

  return (
    <ResponsiveContainer width="100%" height={260}>
      {type === "line" ? (
        <LineChart data={data} margin={{ top: 8, right: 16, bottom: 8, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
          <XAxis dataKey="key" stroke="#94a3b8" fontSize={12} />
          <YAxis stroke="#94a3b8" fontSize={12} />
          <Tooltip contentStyle={{ background: "#1e293b", border: "1px solid #334155" }} />
          <Line type="monotone" dataKey="value" stroke="#6366f1" strokeWidth={2} dot={false} />
        </LineChart>
      ) : type === "pie" ? (
        <PieChart>
          <Tooltip contentStyle={{ background: "#1e293b", border: "1px solid #334155" }} />
          <Pie data={data} dataKey="value" nameKey="key" outerRadius={100} label>
            {data.map((_, i) => (
              <Cell key={i} fill={COLORS[i % COLORS.length]} />
            ))}
          </Pie>
        </PieChart>
      ) : (
        <BarChart data={data} margin={{ top: 8, right: 16, bottom: 8, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
          <XAxis dataKey="key" stroke="#94a3b8" fontSize={12} />
          <YAxis stroke="#94a3b8" fontSize={12} />
          <Tooltip contentStyle={{ background: "#1e293b", border: "1px solid #334155" }} />
          <Bar dataKey="value">
            {data.map((_, i) => (
              <Cell key={i} fill={COLORS[i % COLORS.length]} />
            ))}
          </Bar>
        </BarChart>
      )}
    </ResponsiveContainer>
  );
}
