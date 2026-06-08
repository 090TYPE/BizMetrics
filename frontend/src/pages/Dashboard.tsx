import { useEffect, useState, type FormEvent } from "react";
import { api } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import TopBar from "../components/TopBar";

interface Dataset {
  id: string;
  name: string;
  status: string;
  rowCount: number;
  createdAt: string;
}

export default function Dashboard() {
  const { state } = useAuth();
  const [datasets, setDatasets] = useState<Dataset[]>([]);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    try {
      setDatasets(await api<Dataset[]>("/api/datasets"));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  // Reload whenever the active organization changes (e.g. via the switcher).
  useEffect(() => {
    void load();
  }, [state?.organizationId]);

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
      <TopBar />
      <main>
        <h2>Datasets</h2>
        <p className="hint">
          Tenant-scoped — you only ever see the current organization's data. CSV upload
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
