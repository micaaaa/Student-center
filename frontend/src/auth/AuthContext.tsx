import { createContext, useContext, useEffect, useState, useSyncExternalStore } from 'react';
import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router';
import { errorMessage, getSession, subscribe, validateSession } from '../lib/api';
import type { Role } from '../lib/types';

const AuthContext = createContext({ loading: true, error: '', retry: () => {} });
let validation: Promise<void> | undefined;

export function AuthProvider({ children }: { children: ReactNode }) {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [attempt, setAttempt] = useState(0);

    useEffect(() => {
        let active = true;
        setLoading(true);
        setError('');
        validation ??= validateSession().finally(() => {
            validation = undefined;
        });
        validation
            .catch((error) => {
                if (active && getSession()) setError(errorMessage(error));
            })
            .finally(() => {
                if (active) setLoading(false);
            });
        return () => {
            active = false;
        };
    }, [attempt]);

    return (
        <AuthContext.Provider
            value={{ loading, error, retry: () => setAttempt((value) => value + 1) }}
        >
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    const session = useSyncExternalStore(subscribe, getSession);
    return { ...useContext(AuthContext), user: session?.user ?? null };
}

export function ProtectedRoute({ roles }: { roles?: Role[] }) {
    const { user, loading, error, retry } = useAuth();
    const location = useLocation();
    if (loading)
        return (
            <div className="screen-state" role="status">
                Loading portal…
            </div>
        );
    if (!user) return <Navigate to="/login" state={{ from: location.pathname }} replace />;
    if (error)
        return (
            <div className="screen-state">
                <h1>Connection unavailable</h1>
                <p role="alert">{error}</p>
                <button onClick={retry}>Try again</button>
            </div>
        );
    if (roles && !roles.includes(user.role)) return <Navigate to="/access-denied" replace />;
    return <Outlet />;
}
