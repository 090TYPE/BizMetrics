import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { billing, type BillingStatus } from "../api/client";
import TopBar from "../components/TopBar";

const PLANS = [
  {
    name: "Pro",
    price: "$29/mo",
    features: ["10 members", "50 datasets", "1M rows", "Priority support"],
  },
  {
    name: "Business",
    price: "$99/mo",
    features: ["50 members", "500 datasets", "25M rows", "Dedicated support"],
  },
];

function UsageBar({ used, max, label }: { used: number; max: number; label: string }) {
  const pct = max > 0 ? Math.min(100, (used / max) * 100) : 0;
  const warn = pct >= 80;
  return (
    <div style={{ marginBottom: "0.75rem" }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          fontSize: "0.85rem",
          marginBottom: "0.25rem",
        }}
      >
        <span>{label}</span>
        <span style={{ color: warn ? "#fbbf24" : "var(--muted)" }}>
          {used} / {max}
        </span>
      </div>
      <div
        style={{
          height: 6,
          borderRadius: 999,
          background: "var(--border)",
          overflow: "hidden",
        }}
      >
        <div
          style={{
            width: `${pct}%`,
            height: "100%",
            background: warn ? "#fbbf24" : "var(--accent)",
            borderRadius: 999,
            transition: "width 0.3s",
          }}
        />
      </div>
    </div>
  );
}

export default function Billing() {
  const [status, setStatus] = useState<BillingStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [redirecting, setRedirecting] = useState<string | null>(null);
  const [searchParams] = useSearchParams();

  const success = searchParams.get("success") === "1";

  useEffect(() => {
    billing
      .status()
      .then(setStatus)
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  const startCheckout = async (plan: string) => {
    setRedirecting(plan);
    setError(null);
    try {
      const origin = window.location.origin;
      const { url } = await billing.checkout(
        plan,
        `${origin}/billing?success=1`,
        `${origin}/billing`,
      );
      window.location.href = url;
    } catch (err) {
      setError((err as Error).message);
      setRedirecting(null);
    }
  };

  const openPortal = async () => {
    setRedirecting("portal");
    setError(null);
    try {
      const { url } = await billing.portal(window.location.href);
      window.location.href = url;
    } catch (err) {
      setError((err as Error).message);
      setRedirecting(null);
    }
  };

  const statusColor = (s: string) => {
    if (s === "Active") return "#34d399";
    if (s === "Trialing") return "#60a5fa";
    if (s === "PastDue") return "#fbbf24";
    return "#f87171";
  };

  return (
    <div className="page">
      <TopBar />
      <main>
        <h2>Billing</h2>

        {success && (
          <div
            className="panel"
            style={{ borderColor: "#34d399", marginBottom: "1.5rem" }}
          >
            <p className="notice" style={{ margin: 0 }}>
              🎉 Subscription activated! Your plan will be reflected below.
            </p>
          </div>
        )}

        {loading && <p>Loading…</p>}
        {error && <p className="error">{error}</p>}

        {status && (
          <>
            {/* Current plan */}
            <div className="panel">
              <h3 style={{ marginBottom: "1rem" }}>Current plan</h3>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "1rem",
                  flexWrap: "wrap",
                }}
              >
                <span
                  style={{
                    fontSize: "1.4rem",
                    fontWeight: 700,
                    color: "var(--text)",
                  }}
                >
                  {status.planName}
                </span>
                <span
                  className="badge"
                  style={{ color: statusColor(status.subscriptionStatus) }}
                >
                  {status.subscriptionStatus}
                </span>
                {status.trialDaysLeft !== null && (
                  <span className="hint">
                    {status.trialDaysLeft === 0
                      ? "Trial expired"
                      : `Trial ends in ${status.trialDaysLeft} day${status.trialDaysLeft === 1 ? "" : "s"}`}
                  </span>
                )}
                {status.hasStripeCustomer && (
                  <button
                    className="ghost"
                    style={{ marginLeft: "auto" }}
                    onClick={() => void openPortal()}
                    disabled={redirecting !== null}
                  >
                    {redirecting === "portal" ? "Redirecting…" : "Manage subscription"}
                  </button>
                )}
              </div>

              {!status.stripeConfigured && (
                <p className="hint" style={{ marginTop: "0.75rem", marginBottom: 0 }}>
                  Demo mode — Stripe keys are not configured. Checkout is disabled.
                </p>
              )}
            </div>

            {/* Usage */}
            <div className="panel">
              <h3>Usage</h3>
              <UsageBar
                used={status.usage.datasetsUsed}
                max={status.usage.datasetsMax}
                label="Datasets"
              />
              <UsageBar
                used={status.usage.membersUsed}
                max={status.usage.membersMax}
                label="Team members"
              />
            </div>

            {/* Upgrade options */}
            {status.subscriptionStatus !== "Active" || status.planName === "Free" ? (
              <div>
                <h3 style={{ marginBottom: "0.75rem" }}>Upgrade your plan</h3>
                <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
                  {PLANS.map((p) => (
                    <div
                      key={p.name}
                      className="panel"
                      style={{
                        flex: "1 1 220px",
                        display: "flex",
                        flexDirection: "column",
                        gap: "0.5rem",
                      }}
                    >
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "baseline",
                        }}
                      >
                        <strong style={{ fontSize: "1.1rem" }}>{p.name}</strong>
                        <span style={{ color: "var(--accent)", fontWeight: 600 }}>
                          {p.price}
                        </span>
                      </div>
                      <ul
                        style={{
                          margin: "0.25rem 0",
                          paddingLeft: "1.2rem",
                          color: "var(--muted)",
                          fontSize: "0.85rem",
                        }}
                      >
                        {p.features.map((f) => (
                          <li key={f}>{f}</li>
                        ))}
                      </ul>
                      <button
                        style={{ marginTop: "auto" }}
                        disabled={!status.stripeConfigured || redirecting !== null}
                        onClick={() => void startCheckout(p.name)}
                      >
                        {redirecting === p.name
                          ? "Redirecting to Stripe…"
                          : !status.stripeConfigured
                            ? "Demo mode"
                            : `Upgrade to ${p.name}`}
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
          </>
        )}
      </main>
    </div>
  );
}
