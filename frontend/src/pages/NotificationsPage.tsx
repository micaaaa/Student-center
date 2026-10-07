import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { dateTime } from '../lib/applications';
import { notificationTarget, notificationsChanged, type Notification } from '../lib/notifications';
import { RequestError } from '../components/ApplicationUi';

export function NotificationsPage() {
    const [search, setSearch] = useSearchParams();
    const unreadOnly = search.get('unreadOnly') === 'true';
    const rawPage = Number(search.get('page') || 1);
    const page = Number.isInteger(rawPage) && rawPage > 0 && rawPage <= 100000 ? rawPage : 1;
    const resource = useResource<Notification[]>(
        `/api/notifications/me?unreadOnly=${unreadOnly}&page=${page}&pageSize=20`,
    );
    const [busy, setBusy] = useState(false);
    function refresh() {
        resource.reload();
        notificationsChanged();
    }
    function changePage(next: number) {
        setSearch({ unreadOnly: String(unreadOnly), page: String(next) });
    }
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">INBOX</span>
                    <h1>Notifications</h1>
                    <p className="muted">Updates about your student services and assigned work.</p>
                </div>
                <button className="secondary" onClick={refresh} disabled={busy || resource.loading}>
                    Refresh notifications
                </button>
            </div>
            <div className="list-toolbar">
                <label>
                    Show
                    <select
                        disabled={busy}
                        value={String(unreadOnly)}
                        onChange={(event) =>
                            setSearch({ unreadOnly: event.target.value, page: '1' })
                        }
                    >
                        <option value="false">All notifications</option>
                        <option value="true">Unread only</option>
                    </select>
                </label>
            </div>
            {resource.loading ? (
                <p role="status">Loading notifications…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={refresh} />
            ) : !resource.data?.length ? (
                <section className="panel">
                    <p>
                        {unreadOnly
                            ? 'No unread notifications on this page.'
                            : 'No notifications on this page.'}
                    </p>
                    {page > 1 && (
                        <button className="secondary" onClick={() => changePage(1)}>
                            Go to first page
                        </button>
                    )}
                </section>
            ) : (
                <div className="notification-list">
                    {resource.data.map((item) => (
                        <NotificationCard
                            key={item.id}
                            item={item}
                            disabled={busy}
                            setBusy={setBusy}
                            saved={() => {
                                refresh();
                            }}
                        />
                    ))}
                </div>
            )}
            <div className="button-row application-section">
                <button
                    className="secondary"
                    disabled={busy || resource.loading || page === 1}
                    onClick={() => changePage(page - 1)}
                >
                    Previous
                </button>
                <span>Page {page}</span>
                <button
                    className="secondary"
                    disabled={
                        busy ||
                        resource.loading ||
                        !!resource.error ||
                        resource.data?.length !== 20 ||
                        page >= 100000
                    }
                    onClick={() => changePage(page + 1)}
                >
                    Next
                </button>
            </div>
        </>
    );
}

function NotificationCard({
    item,
    disabled,
    setBusy,
    saved,
}: {
    item: Notification;
    disabled: boolean;
    setBusy: (value: boolean) => void;
    saved: () => void;
}) {
    const { user } = useAuth();
    const [error, setError] = useState('');
    const [saving, setSaving] = useState(false);
    const target = user ? notificationTarget(item, user) : null;
    async function markRead() {
        if (disabled) return;
        setBusy(true);
        setSaving(true);
        setError('');
        try {
            await api('/api/notifications/me/' + encodeURIComponent(item.id) + '/read', {
                method: 'PUT',
            });
            saved();
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
            setSaving(false);
        }
    }
    return (
        <article className={'panel notification-card' + (item.readAtUtc ? '' : ' is-unread')}>
            <div className="heading-row">
                <h2>{item.title}</h2>
                <span className="status-pill">{item.readAtUtc ? 'Read' : 'Unread'}</span>
            </div>
            <p className="muted">{dateTime(item.occurredAtUtc)}</p>
            <p className="preserve-lines">{item.message}</p>
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            <div className="button-row">
                {!item.readAtUtc && (
                    <button className="secondary" disabled={disabled} onClick={markRead}>
                        {saving ? 'Marking as read…' : 'Mark as read'}
                    </button>
                )}
                {target && (
                    <Link className="text-link" to={target.to}>
                        {target.label}
                    </Link>
                )}
            </div>
        </article>
    );
}
