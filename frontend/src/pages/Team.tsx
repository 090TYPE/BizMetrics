import { useEffect, useState, type FormEvent } from "react";
import {
  orgs,
  invitations,
  type Member,
  type Organization,
  type Invitation,
} from "../api/client";
import { useAuth } from "../auth/AuthContext";
import TopBar from "../components/TopBar";

const ROLES = ["Owner", "Admin", "Member", "Viewer"];

export default function Team() {
  const { state } = useAuth();
  const [org, setOrg] = useState<Organization | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [pending, setPending] = useState<Invitation[]>([]);
  const [orgName, setOrgName] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState("Member");
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const canManage = state?.role === "Owner" || state?.role === "Admin";
  // Admins may only grant Member/Viewer; Owners may grant any role.
  const assignable =
    state?.role === "Owner" ? ROLES : ROLES.filter((r) => r !== "Owner" && r !== "Admin");

  const load = async () => {
    setLoading(true);
    try {
      const [o, m] = await Promise.all([orgs.current(), orgs.members()]);
      setOrg(o);
      setOrgName(o.name);
      setMembers(m);
      if (canManage) setPending(await invitations.list());
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  const invite = async (e: FormEvent) => {
    e.preventDefault();
    if (!inviteEmail.trim()) return;
    setNotice(null);
    setError(null);
    try {
      await invitations.create(inviteEmail.trim(), inviteRole);
      setInviteEmail("");
      setNotice(`Invitation sent to ${inviteEmail.trim()} (check the API logs for the link).`);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const revokeInvite = async (id: string) => {
    try {
      await invitations.revoke(id);
      await load();
    } catch (err) {
      setError((err as Error).message);
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

        {canManage && (
          <section className="panel">
            <h3>Invite a member</h3>
            <form className="inline" onSubmit={invite}>
              <input
                type="email"
                placeholder="person@company.com"
                value={inviteEmail}
                onChange={(e) => setInviteEmail(e.target.value)}
              />
              <select value={inviteRole} onChange={(e) => setInviteRole(e.target.value)}>
                {assignable.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
              <button type="submit">Send invite</button>
            </form>
            {notice && <p className="notice">{notice}</p>}

            {pending.length > 0 && (
              <table>
                <thead>
                  <tr>
                    <th>Pending invite</th>
                    <th>Role</th>
                    <th>Expires</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {pending.map((i) => (
                    <tr key={i.id}>
                      <td>{i.email}</td>
                      <td>{i.role}</td>
                      <td>{new Date(i.expiresAt).toLocaleDateString()}</td>
                      <td>
                        <button className="ghost danger" onClick={() => void revokeInvite(i.id)}>
                          Revoke
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
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
          Invitations are emailed asynchronously (the dev sender logs the accept link).
          Role and invite rules are enforced server-side — e.g. the last Owner can't be
          demoted, and an Admin can't invite Owners.
        </p>
      </main>
    </div>
  );
}
