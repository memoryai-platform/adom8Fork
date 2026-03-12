import { Navigate, Route, Routes } from 'react-router-dom';

import './mosaic/css/style.css';

import AppLayout from './components/AppLayout';
import LoginGate from './components/LoginGate';
import { useAgentStatus } from './hooks/useAgentStatus';
import { useAppKey } from './hooks/useAppKey';
import AgentLog from './pages/AgentLog';
import Overview from './pages/Overview';
import StoryDetail from './pages/StoryDetail';
import PageNotFound from './mosaic/pages/utility/PageNotFound';

function LoadingScreen() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-100">
      <div className="rounded-2xl border border-gray-200 bg-white px-6 py-4 text-sm font-medium text-gray-600 shadow-xs">
        Preparing dashboard...
      </div>
    </div>
  );
}

export default function App() {
  const { appKey, clearAppKey, ready, setAppKey } = useAppKey();
  const status = useAgentStatus(appKey, clearAppKey);

  if (!ready) {
    return <LoadingScreen />;
  }

  if (!appKey) {
    return <LoginGate onAuthenticated={setAppKey} />;
  }

  return (
    <Routes>
      <Route
        element={
          <AppLayout
            appKey={appKey}
            connectionStatus={status.connectionStatus}
            data={status.data}
            error={status.error}
            lastUpdated={status.lastUpdated}
            loading={status.loading}
            onLogout={clearAppKey}
          />
        }
      >
        <Route path="/" element={<Overview />} />
        <Route path="/story/:id" element={<StoryDetail />} />
        <Route path="/log" element={<AgentLog />} />
        <Route path="/404" element={<PageNotFound />} />
        <Route path="*" element={<Navigate to="/404" replace />} />
      </Route>
    </Routes>
  );
}
