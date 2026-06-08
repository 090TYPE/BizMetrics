import { useEffect, useRef, useState, type FormEvent } from "react";
import { datasets, type Dataset, type DatasetRows } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import TopBar from "../components/TopBar";

const STATUS_COLORS: Record<Dataset["status"], string> = {
  Pending: "#fbbf24",
  Processing: "#60a5fa",
  Ready: "#34d399",
  Failed: "#f87171",
};

export default function Dashboard() {
  const { state } = useAuth();
  const [items, setItems] = useState<Dataset[]>([]);
  const [name, setName] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [preview, setPreview] = useState<{ id: string; data: DatasetRows } | null>(null);
  const fileInput = useRef<HTMLInputElement>(null);

  const load = async () => {
    try {
      setItems(await datasets.list());
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setLoading(true);
    void load();
  }, [state?.organizationId]);

  // Poll while any dataset is still being processed.
  useEffect(() => {
    if (!items.some((d) => d.status === "Pending" || d.status === "Processing")) return;
    const t = setInterval(load, 1500);
    return () => clearInterval(t);
  }, [items]);

  const upload = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) return;
    setUploading(true);
    setError(null);
    try {
      await datasets.upload(file, name.trim() || file.name);
      setName("");
      setFile(null);
      if (fileInput.current) fileInput.current.value = "";
      await load();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setUploading(false);
    }
  };

  const showRows = async (id: string) => {
    try {
      setPreview({ id, data: await datasets.rows(id, 0, 20) });
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const remove = async (id: string, label: string) => {
    if (!confirm(`Delete dataset "${label}"?`)) return;
    try {
      await datasets.remove(id);
      if (preview?.id === id) setPreview(null);
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
          Upload a CSV — it's stored in object storage and parsed by a background worker.
          The status updates live as processing completes.
        </p>

        <form className="inline" onSubmit={upload}>
          <input
            ref={fileInput}
            type="file"
            accept=".csv,text/csv"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
          <input
            placeholder="Name (optional)"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
          <button type="submit" disabled={!file || uploading}>
            {uploading ? "Uploading…" : "Upload CSV"}
          </button>
        </form>

        {error && <p className="error">{error}</p>}

        {loading ? (
          <p>Loading…</p>
        ) : items.length === 0 ? (
          <p className="hint">No datasets yet — upload a CSV to get started.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
                <th>Rows</th>
                <th>Columns</th>
                <th>Created</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.map((d) => (
                <tr key={d.id}>
                  <td>{d.name}</td>
                  <td>
                    <span style={{ color: STATUS_COLORS[d.status] }}>● {d.status}</span>
                    {d.status === "Failed" && d.errorMessage && (
                      <div className="hint">{d.errorMessage}</div>
                    )}
                  </td>
                  <td>{d.rowCount}</td>
                  <td>{d.columns.length}</td>
                  <td>{new Date(d.createdAt).toLocaleString()}</td>
                  <td className="row-actions">
                    {d.status === "Ready" && (
                      <button className="ghost" onClick={() => void showRows(d.id)}>
                        Preview
                      </button>
                    )}
                    <button className="ghost danger" onClick={() => void remove(d.id, d.name)}>
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {preview && (
          <section className="panel">
            <h3>Preview ({preview.data.total} rows total)</h3>
            <div className="scroll-x">
              <table>
                <thead>
                  <tr>
                    {preview.data.columns.map((c) => (
                      <th key={c}>{c}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {preview.data.rows.map((row, i) => (
                    <tr key={i}>
                      {preview.data.columns.map((c) => (
                        <td key={c}>{row[c]}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}
      </main>
    </div>
  );
}
