import { useEffect, useState, type FormEvent } from "react";
import { api } from "../api/client";
import { useAuth } from "../auth/AuthContext";

interface Dataset {
  id: string;
  name: string;
  status: string;
  rowCount: number;
  createdAt: string;
}

export default function Dashboard() {
  const { logout } = useAuth();
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      setDatasets(await api<Dataset[]>("/api/datasets"));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const create = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    try {
      await api("/api/datasets", { method: "POST", body: JSON.stringify({ name }) });
      setName("");
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="page">
      <header className="topbar">
        <strong>BizMetrics</strong>
        <button className="ghost" onClick={logout}>
          Sign out
        </button>
      </header>

      <main>
        <h2>Datasets</h2>
        <p className="hint">
          Tenant-scoped — you only ever see your own organization's data. CSV upload
          and processing arrive in Phase&nbsp;3; for now create placeholder datasets.
        </p>

        <form className="inline" onSubmit={create}>
          <input
            placeholder="New dataset name"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
          <button type="submit">Add</button>
        </form>

        {error && <p className="error">{error}</p>}
        {loading ? (
          <p>Loading…</p>
        ) : datasets.length === 0 ? (
          <p className="hint">No datasets yet.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
                <th>Rows</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {datasets.map((d) => (
                <tr key={d.id}>
                  <td>{d.name}</td>
                  <td>{d.status}</td>
                  <td>{d.rowCount}</td>
                  <td>{new Date(d.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </main>
    </div>
  );
}
