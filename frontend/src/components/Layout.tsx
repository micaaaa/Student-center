import { useState } from 'react';
import {
    GraduationCap,
    LayoutDashboard,
    UserRound,
    LogOut,
    ArrowUpRight,
    Menu,
    X,
} from 'lucide-react';
import { NavLink, Outlet, useNavigate } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { logout } from '../lib/api';

export const roleLabels = { STUDENT: 'Student', STAFF: 'Staff', ADMIN: 'Administrator' };

export function Layout() {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [open, setOpen] = useState(false);
    const [busy, setBusy] = useState(false);
    if (!user) return null;

    async function signOut() {
        setBusy(true);
        try {
            await logout();
            navigate('/login', { replace: true });
        } catch {
            navigate('/login', {
                replace: true,
                state: {
                    notice: 'You have signed out locally. Session revocation could not be confirmed by the server.',
                },
            });
        } finally {
            setBusy(false);
        }
    }

    return (
        <div className="portal">
            <a className="skip-link" href="#main">
                Skip to content
            </a>
            <header className="mobile-header">
                <span>Student Center</span>
                <button
                    className="icon-button"
                    aria-label={open ? 'Close menu' : 'Open menu'}
                    aria-expanded={open}
                    onClick={() => setOpen(!open)}
                >
                    {open ? <X /> : <Menu />}
                </button>
            </header>
            <aside className={'sidebar ' + (open ? 'is-open' : '')}>
                <NavLink to="/" className="brand" onClick={() => setOpen(false)}>
                    <span className="brand-icon">
                        <GraduationCap />
                    </span>
                    <span>
                        Student
                        <br />
                        Center
                    </span>
                </NavLink>
                <span className="nav-label">NAVIGATION</span>
                <nav aria-label="Main navigation">
                    <NavLink to="/" end onClick={() => setOpen(false)}>
                        <LayoutDashboard size={19} /> Overview
                    </NavLink>
                    {user.role === 'STUDENT' && (
                        <NavLink to="/profile" onClick={() => setOpen(false)}>
                            <UserRound size={19} /> My profile
                        </NavLink>
                    )}
                </nav>
                <div className="sidebar-note">
                    <h3>Account services</h3>
                    <p>
                        {user.role === 'STUDENT'
                            ? 'Manage your personal and academic information.'
                            : 'Review your account details and assigned permissions.'}
                    </p>
                    {user.role === 'STUDENT' && (
                        <NavLink to="/profile" onClick={() => setOpen(false)}>
                            View profile <ArrowUpRight size={16} />
                        </NavLink>
                    )}
                </div>
                <div className="sidebar-account">
                    <span className="avatar">{user.username.slice(0, 2).toUpperCase()}</span>
                    <div>
                        <strong>{user.username}</strong>
                        <small>{roleLabels[user.role]}</small>
                    </div>
                </div>
                <button className="logout" onClick={signOut} disabled={busy}>
                    <LogOut size={18} />
                    {busy ? 'Signing out…' : 'Sign out'}
                </button>
            </aside>
            <div className="workspace">
                <header className="topbar">
                    <span>
                        Student Center <span className="slash">/</span> Portal
                    </span>
                    <span className="role-badge">{roleLabels[user.role]}</span>
                </header>
                <main id="main" className="main-content">
                    <Outlet />
                </main>
                <footer className="portal-footer">
                    Student Center<span>Student Services Portal</span>
                </footer>
            </div>
        </div>
    );
}
