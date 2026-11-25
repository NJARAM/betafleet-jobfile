import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiError } from './api';
import { SessionProvider, useSession } from './session';
import { ToastProvider } from './ui';
import Layout from './Layout';
import { ChangePasswordForm, Login } from './pages/Login';
import Dashboard from './pages/Dashboard';
import Jobs from './pages/Jobs';
import JobDetail from './pages/JobDetail';
import NewJob from './pages/NewJob';
import Handoffs from './pages/Handoffs';
import Clearing from './pages/Clearing';
import Containers from './pages/Containers';
import FillingMasters from './pages/FillingMasters';
import Tracking from './pages/Tracking';
import RoutesPage from './pages/Routes';
import Billing from './pages/Billing';
import Expenses from './pages/Expenses';
import Profitability from './pages/Profitability';
import Portal from './pages/Portal';
import Fleet from './pages/Fleet';
import Workshop from './pages/Workshop';
import Tyres from './pages/Tyres';
import Compliance from './pages/Compliance';
import DriverApp from './pages/DriverApp';
import Users from './pages/Users';
import './styles.css';

const qc = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000, refetchOnWindowFocus: true,
      retry: (n, e) => !(e instanceof ApiError && e.status < 500) && n < 1 // never retry auth or validation failures
    }
  }
});

function App() {
  const { user } = useSession();
  if (!user) return <Login />;
  if (user.mustChangePassword) return <ChangePasswordForm forced />;
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="jobs" element={<Jobs />} />
          <Route path="jobs/:id" element={<JobDetail />} />
          <Route path="new-job" element={<NewJob />} />
          <Route path="handoffs" element={<Handoffs />} />
          <Route path="clearing" element={<Clearing />} />
          <Route path="containers" element={<Containers />} />
          <Route path="filling-masters" element={<FillingMasters />} />
          <Route path="tracking" element={<Tracking />} />
          <Route path="routes" element={<RoutesPage />} />
          <Route path="billing" element={<Billing />} />
          <Route path="expenses" element={<Expenses />} />
          <Route path="profitability" element={<Profitability />} />
          <Route path="portal" element={<Portal />} />
          <Route path="fleet" element={<Fleet />} />
          <Route path="workshop" element={<Workshop />} />
          <Route path="tyres" element={<Tyres />} />
          <Route path="compliance" element={<Compliance />} />
          <Route path="driver" element={<DriverApp />} />
          <Route path="users" element={<Users />} />
          <Route path="*" element={<div className="card">Page not found.</div>} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={qc}>
      <SessionProvider>
        <ToastProvider>
          <App />
        </ToastProvider>
      </SessionProvider>
    </QueryClientProvider>
  </StrictMode>
);
