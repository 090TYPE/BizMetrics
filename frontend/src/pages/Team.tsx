import { useEffect, useState, type FormEvent } from "react";
import {
  orgs,
  type Member,
  type Organization,
} from "../api/client";
import { useAuth } from "../auth/AuthContext";
import TopBar from "../components/TopBar";

const ROLES = ["Owner", "Admin", "Member", "Viewer"];

export default function Team() {
  const { state } = useAuth();
  const [org, setOrg] = useState<Organization | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [orgName, setOrgName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const canManage = state?.role === "Owner" || state?.role === "Admin";

  const load = async () => {
    setLoading(true);
    try {
      const [o, m] = await Promise.all([orgs.current(), orgs.members()]);
      setOrg(o);
      setOrgName(o.name);
      setMembers(m);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [state?.organizationId]);

  const rename = async (e: FormEvent) => {
    e.preventDefault();
    try {
      await orgs.update(orgName.trim());
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const changeRole = async (userId: string, role: string) => {
    try {
      await orgs.changeRole(userId, role);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const remove = async (userId: string, name: string) => {
    if (!confirm(`Remove ${name} from the organization?`)) return;
    try {
      await orgs.removeMember(userId);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="page">
      <TopBar />
      <main>
        <h2>Team</h2>
        {org && (
          <p className="hint">
            {org.name} · {org.subscriptionStatus}
            {org.planName ? ` · ${org.planName} plan` : ""}
          </p>
        )}

        {canManage && (
          <form className="inline" onSubmit={rename}>
            <input
              value={orgName}
              onChange={(e) => setOrgName(e.target.value)}
              placeholder="Organization name"
            />
            <button type="submit">Rename</button>
          </form>
        )}

        {error && <p className="error">{error}</p>}

        {loading ? (
          <p>Loading…</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Role</th>
                {canManage && <th></th>}
              </tr>
            </thead>
            <tbody>
              {members.map((m) => {
                const isSelf = m.userId === state?.userId;
                return (
                  <tr key={m.userId}>
                    <td>
                      {m.fullName}
                      {isSelf && <span className="badge"> you</span>}
                    </td>
                    <td>{m.email}</td>
                    <td>
                      {canManage && !isSelf ? (
                        <select
                          value={m.role}
                          onChange={(e) => void changeRole(m.userId, e.target.value)}
                        >
                          {ROLES.map((r) => (
                            <option key={r} value={r}>
                              {r}
                            </option>
                          ))}
                        </select>
                      ) : (
                        m.role
                      )}
                    </td>
                    {canManage && (
                      <td>
                        {!isSelf && (
                          <button
                            className="ghost danger"
                            onClick={() => void remove(m.userId, m.fullName)}
                          >
                            Remove
                          </button>
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}

        <p className="hint">
          Email invitations for new members arrive in Phase&nbsp;2. Role changes are
          enforced server-side (e.g. the last Owner can't be demoted).
        </p>
      </main>
    </div>
  );
}
