import { useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { invitations, orgs, type InvitationPreview } from "../api/client";
import { useAuth } from "../auth/AuthContext";

export default function Accept() {
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const { isAuthenticated, switchOrg, state } = useAuth();
  const navigate = useNavigate();

  const [preview, setPreview] = useState<InvitationPreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!token) {
      setError("Missing invitation token.");
      return;
    }
    invitations
      .preview(token)
      .then(setPreview)
      .catch(() => setError("This invitation could not be found."));
  }, [token]);

  const accept = async () => {
    setBusy(true);
    setError(null);
    try {
      await invitations.accept(token);
      // Surface the new org immediately by switching into it if we can find it.
      const mine = await orgs.mine();
      const joined = mine.find((o) => o.name === preview?.organizationName);
      if (joined && joined.id !== state?.organizationId) await switchOrg(joined.id);
      navigate("/team");
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card">
      <h1>Team invitation</h1>

      {error && <p className="error">{error}</p>}

      {preview && (
        <>
          <p>
            You've been invited to join <strong>{preview.organizationName}</strong> as{" "}
            <strong>{preview.role}</strong>.
          </p>
          {!preview.redeemable && (
            <p className="error">This invitation has expired or was revoked.</p>
          )}

          {preview.redeemable &&
            (isAuthenticated ? (
              <button onClick={() => void accept()} disabled={busy}>
                {busy ? "Joining…" : "Accept invitation"}
              </button>
            ) : (
              <p className="hint">
                Sign in as <strong>{preview.email}</strong> to accept.{" "}
                <Link to={`/login?next=${encodeURIComponent(`/accept?token=${token}`)}`}>
                  Sign in
                </Link>{" "}
                or{" "}
                <Link to={`/register?next=${encodeURIComponent(`/accept?token=${token}`)}`}>
                  create an account
                </Link>
                .
              </p>
            ))}
        </>
      )}
    </div>
  );
}
