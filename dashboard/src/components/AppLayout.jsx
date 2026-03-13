import { Outlet, useLocation } from 'react-router-dom';
import { useMemo, useState } from 'react';

import AppHeader from './AppHeader';
import AppSidebar from './AppSidebar';

function getPageTitle(pathname) {
  if (pathname.startsWith('/story/')) {
    return 'Story Detail';
  }

  if (pathname === '/log') {
    return 'Agent Log';
  }

  return 'Overview';
}

export default function AppLayout(props) {
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const pageTitle = useMemo(() => getPageTitle(location.pathname), [location.pathname]);

  return (
    <div className="flex h-[100dvh] overflow-hidden">
      <AppSidebar
        appKey={props.appKey}
        codebaseData={props.codebaseData}
        codebaseError={props.codebaseError}
        codebaseLoading={props.codebaseLoading}
        healthData={props.healthData}
        healthError={props.healthError}
        healthLoading={props.healthLoading}
        onUnauthorized={props.onLogout}
        refreshCodebase={props.refreshCodebase}
        refreshHealth={props.refreshHealth}
        refreshStatus={props.refreshStatus}
        sidebarOpen={sidebarOpen}
        setSidebarOpen={setSidebarOpen}
      />

      <div className="relative flex flex-1 flex-col overflow-y-auto overflow-x-hidden">
        <AppHeader
          connectionStatus={props.connectionStatus}
          lastUpdated={props.lastUpdated}
          onLogout={props.onLogout}
          pageTitle={pageTitle}
          sidebarOpen={sidebarOpen}
          setSidebarOpen={setSidebarOpen}
        />

        <main className="grow">
          <div className="mx-auto w-full max-w-[96rem] px-4 py-8 sm:px-6 lg:px-8">
            <Outlet
              context={{
                appKey: props.appKey,
                codebaseData: props.codebaseData,
                codebaseError: props.codebaseError,
                codebaseLoading: props.codebaseLoading,
                connectionStatus: props.connectionStatus,
                data: props.data,
                error: props.error,
                healthData: props.healthData,
                healthError: props.healthError,
                healthLoading: props.healthLoading,
                loading: props.loading,
                onUnauthorized: props.onLogout,
                refreshCodebase: props.refreshCodebase,
                refreshHealth: props.refreshHealth,
                refreshStatus: props.refreshStatus,
              }}
            />
          </div>
        </main>
      </div>
    </div>
  );
}
