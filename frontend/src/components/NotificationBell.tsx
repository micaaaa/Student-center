import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { Bell } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { api } from '../lib/api';

export function NotificationBell() {
    const { user } = useAuth();
    const [count, setCount] = useState<number | null>(null);
    const [failed, setFailed] = useState(false);
    useEffect(() => {
        let active = true;
        let controller: AbortController | undefined;
        setCount(null);
        setFailed(false);
        async function refresh() {
            if (document.hidden) return;
            controller?.abort();
            controller = new AbortController();
            const current = controller;
            try {
                const result = await api<{ count: number }>('/api/notifications/me/unread-count', {
                    signal: current.signal,
                });
                if (active && !current.signal.aborted) {
                    setCount(result.count);
                    setFailed(false);
                }
            } catch {
                if (active && !current.signal.aborted) {
                    setCount(null);
                    setFailed(true);
                }
            }
        }
        void refresh();
        const timer = window.setInterval(refresh, 60000);
        window.addEventListener('notifications-changed', refresh);
        window.addEventListener('focus', refresh);
        document.addEventListener('visibilitychange', refresh);
        return () => {
            active = false;
            controller?.abort();
            clearInterval(timer);
            window.removeEventListener('notifications-changed', refresh);
            window.removeEventListener('focus', refresh);
            document.removeEventListener('visibilitychange', refresh);
        };
    }, [user?.id]);
    return (
        <Link
            className="notification-bell"
            to="/notifications"
            aria-label={
                failed
                    ? 'Notifications — unread count unavailable'
                    : count === null
                      ? 'Notifications'
                      : `Notifications, ${count} unread`
            }
            title={
                failed ? 'Unread count unavailable. Open notifications to retry.' : 'Notifications'
            }
        >
            <Bell size={21} aria-hidden="true" />
            {failed ? (
                <span className="notification-count">!</span>
            ) : (
                count !== null &&
                count > 0 && (
                    <span className="notification-count">{count > 99 ? '99+' : count}</span>
                )
            )}
        </Link>
    );
}
