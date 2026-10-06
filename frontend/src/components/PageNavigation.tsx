import { useEffect, useRef } from 'react';
import { ArrowLeft, Home } from 'lucide-react';
import { Link, useLocation, useNavigate, useNavigationType } from 'react-router';
import { useAuth } from '../auth/AuthContext';

function parentPage(path: string): string {
    if (path.startsWith('/staff/accommodations/')) return '/staff/assignments';
    const parts = path.split('/').filter(Boolean);
    const rootLength = parts[0] === 'staff' ? 2 : 1;
    if (parts.length <= rootLength) return '/';
    if (parts.includes('charges')) return parts[0] === 'staff' ? '/staff/billing' : '/my-billing';
    return '/' + parts.slice(0, -1).join('/');
}

interface Visit {
    key: string;
    previous?: string;
}

export function PageNavigation() {
    const location = useLocation();
    const navigationType = useNavigationType();
    const navigate = useNavigate();
    const { user } = useAuth();
    const lastVisit = useRef<Visit | null>(null);
    const storageKey = 'student-center.navigation.' + user?.id;
    let visits: Record<string, string | undefined> = {};
    try {
        visits = JSON.parse(sessionStorage.getItem(storageKey) || '{}');
    } catch {
        // Navigation still works when browser storage is unavailable.
    }
    let previous = visits[location.key];
    if (lastVisit.current && lastVisit.current.key !== location.key) {
        if (navigationType === 'PUSH') previous = lastVisit.current.key;
        if (navigationType === 'REPLACE') previous = lastVisit.current.previous;
    }
    if (lastVisit.current?.key === location.key) previous = lastVisit.current.previous;

    useEffect(() => {
        lastVisit.current = { key: location.key, previous };
        try {
            const saved = JSON.parse(sessionStorage.getItem(storageKey) || '{}');
            saved[location.key] = previous ?? null;
            sessionStorage.setItem(storageKey, JSON.stringify(saved));
        } catch {
            // The parent-page fallback remains available without storage.
        }
    }, [location.key, previous, storageKey]);

    if (location.pathname === '/') return null;
    return (
        <nav className="page-navigation" aria-label="Page navigation">
            <button
                className="secondary"
                type="button"
                onClick={() =>
                    previous
                        ? navigate(-1)
                        : navigate(parentPage(location.pathname), { replace: true })
                }
            >
                <ArrowLeft size={18} aria-hidden="true" /> Back
            </button>
            <Link to="/" className="text-link">
                <Home size={16} aria-hidden="true" />
                Home
            </Link>
        </nav>
    );
}
