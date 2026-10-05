import { BrowserRouter, Link, Route, Routes } from 'react-router';
import { AuthProvider, ProtectedRoute } from './auth/AuthContext';
import { Layout } from './components/Layout';
import { AuthPage } from './pages/AuthPage';
import { Dashboard } from './pages/Dashboard';
import { ProfilePage } from './pages/ProfilePage';

export default function App() {
    return (
        <BrowserRouter>
            <AuthProvider>
                <Routes>
                    <Route path="/login" element={<AuthPage key="login" />} />
                    <Route path="/register" element={<AuthPage key="register" register />} />
                    <Route element={<ProtectedRoute />}>
                        <Route element={<Layout />}>
                            <Route index element={<Dashboard />} />
                            <Route element={<ProtectedRoute roles={['STUDENT']} />}>
                                <Route path="/profile" element={<ProfilePage />} />
                            </Route>
                            <Route
                                path="/access-denied"
                                element={
                                    <div className="panel">
                                        <h1>Access denied</h1>
                                        <Link to="/">Return to overview</Link>
                                    </div>
                                }
                            />
                        </Route>
                    </Route>
                    <Route
                        path="*"
                        element={
                            <div className="screen-state">
                                <h1>Page not found</h1>
                                <Link to="/">Return to home</Link>
                            </div>
                        }
                    />
                </Routes>
            </AuthProvider>
        </BrowserRouter>
    );
}
