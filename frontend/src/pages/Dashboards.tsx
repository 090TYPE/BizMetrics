import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { dashboards, type DashboardSummary } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import TopBar from "../components/TopBar";

export default function Dashboards() {
  const { state } = useAuth();
  const [items, setItems] = useState<DashboardSummary[]>([]);
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      setItems(await dashboards.list());
    } catch (err) {
      setError((err as Error).message);
    }
  };

  useEffect(() => {
    void load();
  }, [state?.organizationId]);

  const create = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    try {
      await dashboards.create(name.trim());
      setName("");
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const remove = async (id: string, label: string) => {
    if (!confirm(`Delete dashboard "${label}"?`)) return;
    try {
      await dashboards.remove(id);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="page">
      <TopBar />
      <main>
        <h2>Dashboards</h2>
        <p className="hint">
          Create a dashboard, then save charts to it from a dataset's Explore view.
        </p>

        <form className="inline" onSubmit={create}>
          <input placeholder="New dashboard name" value={name} onChange={(e) => setName(e.target.value)} />
          <button type="submit">Create</button>
        </form>

        {error && <p className="error">{error}</p>}

        {items.length === 0 ? (
          <p className="hint">No dashboards yet.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Widgets</th>
                <th>Created</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((d) => (
                <tr key={d.id}>
                  <td>
                    <Link to={`/dashboards/${d.id}`}>{d.name}</Link>
                  </td>
                  <td>{d.widgetCount}</td>
                  <td>{new Date(d.createdAt).toLocaleDateString()}</td>
                  <td>
                    <button className="ghost danger" onClick={() => void remove(d.id, d.name)}>
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </main>
    </div>
  );
}
