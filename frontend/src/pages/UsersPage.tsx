import { useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import type { User, Role } from '../lib/types';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { ConfirmationDialog } from '../components/ConfirmationDialog';
import { roleLabels } from '../components/Layout';

const permissions = {
    ManageApplications: 'Competitions and applications',
    ManageAccommodation: 'Accommodation',
    ManageFood: 'Dining',
    ManageMaintenance: 'Maintenance',
    ManageBilling: 'Billing',
    ManageUsers: 'User administration',
};

export function UsersPage() {
    const resource = useResource<User[]>('/api/users');
    const [params, setParams] = useSearchParams();
    const query = params.get('search') || '';
    const role = params.get('role') || '';
    const status = params.get('status') || '';
    const rawPage = Number(params.get('page') || 1);
    const page = Number.isInteger(rawPage) && rawPage > 0 ? rawPage : 1;
    function filter(key: string, value: string) {
        setParams(
            (current) => {
                const next = new URLSearchParams(current);
                next.set(key, value);
                next.set('page', '1');
                return next;
            },
            { replace: true },
        );
    }
    const users =
        resource.data?.filter(
            (user) =>
                (!role || user.role === role) &&
                (!status || user.status === status) &&
                `${user.username} ${user.email}`.toLowerCase().includes(query.trim().toLowerCase()),
        ) || [];
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">ADMINISTRATION</span>
                <h1>Users</h1>
                <p className="muted">Manage existing accounts and access to student services.</p>
            </div>
            <div className="list-toolbar">
                <label>
                    Username or email
                    <input
                        type="search"
                        value={query}
                        onChange={(event) => filter('search', event.target.value)}
                    />
                </label>
                <label>
                    Role
                    <select value={role} onChange={(event) => filter('role', event.target.value)}>
                        <option value="">All roles</option>
                        {Object.entries(roleLabels).map(([value, label]) => (
                            <option key={value} value={value}>
                                {label}
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Status
                    <select
                        value={status}
                        onChange={(event) => filter('status', event.target.value)}
                    >
                        <option value="">All statuses</option>
                        <option value="ACTIVE">Active</option>
                        <option value="INACTIVE">Inactive</option>
                    </select>
                </label>
            </div>
            {resource.loading ? (
                <p role="status">Loading users…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : (
                <section className="panel">
                    <p>{users.length} accounts found.</p>
                    {users.slice((page - 1) * 20, page * 20).map((user) => (
                        <article className="student-result" key={user.id}>
                            <div>
                                <strong>{user.username}</strong>
                                <p>{user.email}</p>
                                <p>
                                    {roleLabels[user.role]} ·{' '}
                                    {user.status === 'ACTIVE' ? 'Active' : 'Inactive'}
                                </p>
                            </div>
                            <Link className="text-link" to={'/staff/users/' + user.id}>
                                Manage account
                            </Link>
                        </article>
                    ))}
                    {users.length === 0 && <p>Try another search or filter.</p>}
                    <div className="button-row application-section">
                        <button
                            className="secondary"
                            disabled={page === 1}
                            onClick={() =>
                                setParams((current) => {
                                    const next = new URLSearchParams(current);
                                    next.set('page', String(page - 1));
                                    return next;
                                })
                            }
                        >
                            Previous
                        </button>
                        <span>Page {page}</span>
                        <button
                            className="secondary"
                            disabled={page * 20 >= users.length}
                            onClick={() =>
                                setParams((current) => {
                                    const next = new URLSearchParams(current);
                                    next.set('page', String(page + 1));
                                    return next;
                                })
                            }
                        >
                            Next
                        </button>
                    </div>
                </section>
            )}
        </>
    );
}
export function UserPage() {
    const { id } = useParams();
    const [notice, setNotice] = useState('');
    const resource = useResource<User>('/api/users/' + id);
    if (resource.loading) return <p role="status">Loading account…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    return (
        <>
            {notice && (
                <p className="notice success" role="status">
                    {notice}
                </p>
            )}
            <UserEditor
                key={
                    resource.data!.id +
                    resource.data!.role +
                    resource.data!.status +
                    resource.data!.permissions.join(',')
                }
                account={resource.data!}
                saved={(updated) => {
                    resource.setData(updated);
                    setNotice('Account updated.');
                }}
            />
        </>
    );
}
function UserEditor({ account, saved }: { account: User; saved: (user: User) => void }) {
    const { user } = useAuth();
    const own = user?.id === account.id;
    const [role, setRole] = useState<Role>(account.role);
    const [selected, setSelected] = useState(account.permissions);
    const [pending, setPending] = useState<
        'role' | 'permissions' | 'activate' | 'deactivate' | null
    >(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function confirm() {
        if (!pending || busy) return;
        setBusy(true);
        setError('');
        try {
            const result = await api<User>(`/api/users/${account.id}/${pending}`, {
                method: 'PUT',
                ...(pending === 'role'
                    ? { body: JSON.stringify({ role }) }
                    : pending === 'permissions'
                      ? { body: JSON.stringify({ permissions: selected }) }
                      : {}),
            });
            saved(result);
            setPending(null);
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">USER ACCOUNT</span>
                <h1>{account.username}</h1>
                <p>{account.email}</p>
                <StatusBadge status={account.status} />
            </div>
            {own && (
                <p className="notice">
                    This is your account. Another authorized administrator must change your access
                    or account status.
                </p>
            )}
            <section className="panel">
                <h2>Account role</h2>
                <label>
                    Role
                    <select
                        disabled={own || busy}
                        value={role}
                        onChange={(event) => setRole(event.target.value as Role)}
                    >
                        {Object.entries(roleLabels).map(([value, label]) => (
                            <option key={value} value={value}>
                                {label}
                            </option>
                        ))}
                    </select>
                </label>
                <p className="muted">
                    Roles determine which services the account can use. Staff permissions are
                    configured separately. Changing to Student removes staff permissions.
                </p>
                <button
                    className="primary"
                    disabled={own || busy || role === account.role}
                    onClick={() => {
                        setError('');
                        setPending('role');
                    }}
                >
                    Save role
                </button>
            </section>
            <section className="panel application-section">
                <h2>Service permissions</h2>
                <fieldset
                    className="competition-fields"
                    disabled={own || busy || account.role === 'STUDENT'}
                >
                    {Object.entries(permissions).map(([value, label]) => (
                        <label className="permission-option" key={value}>
                            <input
                                type="checkbox"
                                checked={selected.includes(value)}
                                onChange={(event) =>
                                    setSelected(
                                        event.target.checked
                                            ? [...selected, value]
                                            : selected.filter((item) => item !== value),
                                    )
                                }
                            />
                            {label}
                        </label>
                    ))}
                </fieldset>
                {account.role === 'STUDENT' && (
                    <p>Student accounts cannot receive staff permissions.</p>
                )}
                <button
                    className="primary"
                    disabled={
                        own ||
                        busy ||
                        account.role === 'STUDENT' ||
                        [...selected].sort().join() === [...account.permissions].sort().join()
                    }
                    onClick={() => {
                        setError('');
                        setPending('permissions');
                    }}
                >
                    Save permissions
                </button>
            </section>
            <section className="panel application-section">
                <h2>Account status</h2>
                <p>
                    Inactive accounts cannot sign in or refresh their session. Existing access
                    tokens in other services remain valid until they expire.
                </p>
                <button
                    className="secondary"
                    disabled={own || busy}
                    onClick={() => {
                        setError('');
                        setPending(account.status === 'ACTIVE' ? 'deactivate' : 'activate');
                    }}
                >
                    {account.status === 'ACTIVE' ? 'Deactivate account' : 'Activate account'}
                </button>
            </section>
            {pending && (
                <ConfirmationDialog
                    title="Confirm account change"
                    busy={busy}
                    onClose={() => setPending(null)}
                    onConfirm={confirm}
                >
                    <p>
                        {account.username} · {account.email}
                    </p>
                    <p>
                        {pending === 'role'
                            ? `Change role to ${roleLabels[role]}?`
                            : pending === 'permissions'
                              ? `Allow access to: ${selected.map((value) => permissions[value as keyof typeof permissions] || value).join(', ') || 'no staff services'}?`
                              : pending === 'deactivate'
                                ? 'Deactivate this account?'
                                : 'Activate this account?'}
                    </p>
                    {error && (
                        <p className="notice error" role="alert">
                            {error}
                        </p>
                    )}
                </ConfirmationDialog>
            )}
        </>
    );
}
