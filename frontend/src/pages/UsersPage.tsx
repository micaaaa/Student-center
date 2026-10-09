import { useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { StudentsPage } from './StudentsPage';
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
    const { user } = useAuth();
    return user?.permissions.includes('ManageUsers') ? <AccountList /> : <StudentsPage />;
}

interface StudentDirectoryEntry {
    id: string;
    userId: string;
    firstName: string;
    lastName: string;
    studentNumber: string;
}

function AccountList() {
    const profiles = useResource<StudentDirectoryEntry[]>('/api/students/directory');
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
                `${user.username} ${user.email} ${
                    profiles.data
                        ?.filter((profile) => profile.userId === user.id)
                        .map(
                            (profile) =>
                                `${profile.firstName} ${profile.lastName} ${profile.studentNumber}`,
                        )
                        .join(' ') ?? ''
                }`
                    .toLowerCase()
                    .includes(query.trim().toLowerCase()),
        ) || [];
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">ADMINISTRATION</span>
                <h1>Users</h1>
                <p className="muted">Manage existing accounts and access to student services.</p>
            </div>
            {profiles.error && <RequestError error={profiles.error} retry={profiles.reload} />}
            <div className="list-toolbar">
                <label>
                    Name, student number, username or email
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
                                {profiles.data
                                    ?.filter((profile) => profile.userId === user.id)
                                    .map((profile) => (
                                        <p key={profile.id}>
                                            {profile.firstName} {profile.lastName} ·{' '}
                                            {profile.studentNumber}
                                        </p>
                                    ))}
                                <p>{user.email}</p>
                                <p>
                                    {roleLabels[user.role]} ·{' '}
                                    {user.status === 'ACTIVE' ? 'Active' : 'Inactive'}
                                </p>
                            </div>
                            {user.role === 'STUDENT' &&
                                profiles.data?.find((profile) => profile.userId === user.id) && (
                                    <Link
                                        className="text-link"
                                        to={
                                            '/staff/students/' +
                                            profiles.data.find(
                                                (profile) => profile.userId === user.id,
                                            )!.id
                                        }
                                    >
                                        View student profile
                                    </Link>
                                )}
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
    const navigate = useNavigate();
    const [confirmation, setConfirmation] = useState('');
    const profiles = useResource<StudentDirectoryEntry[]>('/api/students/directory');
    const student = profiles.data?.find((profile) => profile.userId === account.id);
    const { user } = useAuth();
    const own = user?.id === account.id;
    const [role, setRole] = useState<Role>(account.role);
    const [selected, setSelected] = useState(account.permissions);
    const [pending, setPending] = useState<
        'role' | 'permissions' | 'activate' | 'deactivate' | 'delete' | null
    >(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function confirm() {
        if (!pending || busy) return;
        if (pending === 'delete' && confirmation !== account.username) {
            setError('Enter the exact username to confirm deletion.');
            return;
        }
        setBusy(true);
        setError('');
        try {
            if (pending === 'delete') {
                await api('/api/users/' + account.id, {
                    method: 'DELETE',
                    body: JSON.stringify({ username: confirmation }),
                });
                navigate('/staff/users', { replace: true });
                return;
            }
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
            {account.role === 'STUDENT' && student && (
                <Link className="secondary" to={'/staff/students/' + student.id}>
                    View student profile
                </Link>
            )}
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
                    Inactive accounts cannot sign in or access services. Role and permission changes
                    apply to subsequent requests.
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
            {user?.role === 'ADMIN' && !own && (
                <section className="panel application-section">
                    <h2>Delete account</h2>
                    <p>
                        Remove account access and personal profile details. Linked financial and
                        service records are retained without the account holder's profile details.
                    </p>
                    <button
                        className="secondary"
                        disabled={busy}
                        onClick={() => {
                            setError('');
                            setConfirmation('');
                            setPending('delete');
                        }}
                    >
                        Delete account
                    </button>
                </section>
            )}
            {pending && (
                <ConfirmationDialog
                    title={pending === 'delete' ? 'Delete account' : 'Confirm account change'}
                    busy={busy}
                    onClose={() => setPending(null)}
                    onConfirm={confirm}
                >
                    <p>
                        {account.username} · {account.email}
                    </p>
                    <p>
                        {pending === 'delete'
                            ? 'This cannot be undone. Open accommodation, charges and repair assignments remain for staff to resolve.'
                            : pending === 'role'
                              ? `Change role to ${roleLabels[role]}?`
                              : pending === 'permissions'
                                ? `Allow access to: ${selected.map((value) => permissions[value as keyof typeof permissions] || value).join(', ') || 'no staff services'}?`
                                : pending === 'deactivate'
                                  ? 'Deactivate this account?'
                                  : 'Activate this account?'}
                    </p>
                    {pending === 'delete' && (
                        <label>
                            Type {account.username} to confirm
                            <input
                                value={confirmation}
                                onChange={(event) => setConfirmation(event.target.value)}
                                autoComplete="off"
                            />
                        </label>
                    )}
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
