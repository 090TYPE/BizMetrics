import { useEffect, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { orgs, type MyOrganization } from "../api/client";
import { useAuth } from "../auth/AuthContext";

export default function TopBar() {
  const { state, switchOrg, logout } = useAuth();
  const location = useLocation();
  const [mine, setMine] = useState<MyOrganization[]>([]);

  useEffect(() => {
    void orgs.mine().then(setMine).catch(() => setMine([]));
  }, [state?.organizationId]);

  const onSwitch = async (orgId: string) => {
    if (orgId !== state?.organizationId) {
      await switchOrg(orgId);
      // Re-fetch of page data is driven by the org id changing in context.
    }
  };

  const link = (to: string, label: string) => (
    <Link to={to} className={location.pathname === to ? "nav active" : "nav"}>
      {label}
    </Link>
  );

  return (
    <header className="topbar">
      <div className="topbar-left">
        <strong>BizMetrics</strong>
        {mine.length > 0 && (
          <select
            value={state?.organizationId ?? ""}
            onChange={(e) => void onSwitch(e.target.value)}
            title="Switch organization"
          >
            {mine.map((o) => (
              <option key={o.id} value={o.id}>
                {o.name}
              </option>
            ))}
          </select>
        )}
        {state?.role && <span className="badge">{state.role}</span>}
      </div>

      <nav className="topbar-right">
        {link("/", "Datasets")}
        {link("/dashboards", "Dashboards")}
        {link("/team", "Team")}
        <button className="ghost" onClick={logout}>
          Sign out
        </button>
      </nav>
    </header>
  );
}
