import { NavLink } from 'react-router-dom';

import packageJson from '../../package.json';

function NavItem({ to, label, icon, end = false }) {
  return (
    <li>
      <NavLink
        end={end}
        to={to}
        className={({ isActive }) => `flex items-center rounded-lg px-3 py-2 text-sm font-medium transition ${
          isActive ? 'bg-ado-500/[0.12] text-ado-600' : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900'
        }`}
      >
        {({ isActive }) => (
          <>
            <span className={`mr-3 transition ${isActive ? 'text-ado-500' : 'text-gray-400'}`}>{icon}</span>
            <span>{label}</span>
          </>
        )}
      </NavLink>
    </li>
  );
}

export default function AppSidebar({ sidebarOpen, setSidebarOpen }) {
  return (
    <div className="min-w-fit">
      <div
        className={`fixed inset-0 z-40 bg-gray-900/30 transition-opacity duration-200 lg:hidden ${
          sidebarOpen ? 'opacity-100' : 'pointer-events-none opacity-0'
        }`}
        aria-hidden="true"
        onClick={() => setSidebarOpen(false)}
      />

      <aside
        id="sidebar"
        className={`absolute left-0 top-0 z-40 flex h-[100dvh] w-64 shrink-0 -translate-x-64 flex-col overflow-y-auto rounded-r-2xl bg-white p-4 shadow-xs transition-all duration-200 ease-in-out lg:static lg:translate-x-0 ${
          sidebarOpen ? 'translate-x-0' : ''
        }`}
      >
        <div className="mb-8 flex items-center justify-between pr-3 sm:px-2">
          <button
            className="text-gray-500 hover:text-gray-400 lg:hidden"
            onClick={() => setSidebarOpen(false)}
            aria-controls="sidebar"
            aria-expanded={sidebarOpen}
          >
            <span className="sr-only">Close sidebar</span>
            <svg className="h-6 w-6 fill-current" viewBox="0 0 24 24">
              <path d="M10.7 18.7l1.4-1.4L7.8 13H20v-2H7.8l4.3-4.3-1.4-1.4L4 12z" />
            </svg>
          </button>
          <NavLink end to="/" className="flex items-center gap-3">
            <img src="/brand/logo-option-chunky-infinity-box.svg" className="h-10 w-10 rounded-2xl" alt="ADOm8 logo" />
            <span>
              <span className="block text-sm font-semibold text-gray-900">ADOm8</span>
              <span className="block text-xs uppercase tracking-[0.2em] text-gray-400">Dashboard</span>
            </span>
          </NavLink>
        </div>

        <div className="space-y-8">
          <div>
            <h3 className="pl-3 text-xs font-semibold uppercase text-gray-400">Navigation</h3>
            <ul className="mt-3 space-y-1">
              <NavItem
                to="/"
                end
                label="Overview"
                icon={
                  <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
                    <path d="M5.936.278A7.983 7.983 0 0 1 8 0a8 8 0 1 1-8 8c0-.722.104-1.413.278-2.064a1 1 0 1 1 1.932.516A5.99 5.99 0 0 0 2 8a6 6 0 1 0 6-6c-.53 0-1.045.076-1.548.21A1 1 0 1 1 5.936.278Z" />
                    <path d="M6.068 7.482A2.003 2.003 0 0 0 8 10a2 2 0 1 0-.518-3.932L3.707 2.293a1 1 0 0 0-1.414 1.414l3.775 3.775Z" />
                  </svg>
                }
              />
              <NavItem
                to="/log"
                label="Agent Log"
                icon={
                  <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
                    <path d="M2 2h12v2H2zM2 7h12v2H2zM2 12h12v2H2z" />
                  </svg>
                }
              />
            </ul>
          </div>
        </div>

        <div className="mt-auto rounded-xl border border-gray-200 bg-gray-50 p-4">
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-gray-400">ADOm8</div>
          <div className="mt-2 text-sm font-medium text-gray-800">Version {packageJson.version}</div>
          <a
            href="https://adom8.dev"
            target="_blank"
            rel="noreferrer"
            className="mt-3 inline-flex text-sm font-medium text-ado-500 hover:text-ado-600"
          >
            adom8.dev
          </a>
        </div>
      </aside>
    </div>
  );
}
