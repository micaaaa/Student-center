import { NotificationBell } from './NotificationBell';
import { PageNavigation } from './PageNavigation';
import { useState } from 'react';
import {
    GraduationCap,
    Bell,
    LayoutDashboard,
    UserRound,
    LogOut,
    ChevronDown,
    Menu,
    X,
} from 'lucide-react';
import { NavLink, Outlet, useNavigate, useLocation } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { logout } from '../lib/api';
import { portalServices, serviceLinkActive } from '../lib/navigation';

export const roleLabels = { STUDENT: 'Student', STAFF: 'Staff', ADMIN: 'Administrator' };

export function Layout() {
    const { user } = useAuth();
    const navigate = useNavigate();
    const { pathname } = useLocation();
    const [open, setOpen] = useState(false);
    const [busy, setBusy] = useState(false);
    if (!user) return null;
    const services = portalServices(user);

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
                <span className="nav-label">SERVICES</span>
                <nav aria-label="Main navigation">
                    <NavLink to="/" end onClick={() => setOpen(false)}>
                        <LayoutDashboard size={19} /> Home
                    </NavLink>
                    <NavLink to="/notifications" onClick={() => setOpen(false)}>
                        <Bell size={19} /> Notifications
                    </NavLink>
                    {services.map(({ id, title, icon: Icon, links }) => {
                        const active = links.some((link) =>
                            serviceLinkActive(pathname, link.to, services),
                        );
                        if (id === 'students')
                            return (
                                <NavLink
                                    key={id}
                                    to="/staff/students"
                                    onClick={() => setOpen(false)}
                                >
                                    <Icon size={19} />
                                    Students
                                </NavLink>
                            );
                        return (
                            <details className="nav-group" key={id + pathname} open={active}>
                                <summary>
                                    <Icon size={19} aria-hidden="true" />
                                    <span>{title}</span>
                                    <ChevronDown
                                        size={15}
                                        className="nav-chevron"
                                        aria-hidden="true"
                                    />
                                </summary>
                                <div className="nav-group-links">
                                    {links.map((link) => (
                                        <NavLink
                                            key={link.to}
                                            to={link.to}
                                            aria-current={
                                                serviceLinkActive(pathname, link.to, services)
                                                    ? 'page'
                                                    : false
                                            }
                                            className={() =>
                                                serviceLinkActive(pathname, link.to, services)
                                                    ? 'active'
                                                    : ''
                                            }
                                            onClick={() => setOpen(false)}
                                        >
                                            {link.label}
                                        </NavLink>
                                    ))}
                                </div>
                            </details>
                        );
                    })}
                    {user.role === 'STUDENT' && (
                        <NavLink to="/profile" onClick={() => setOpen(false)}>
                            <UserRound size={19} /> My profile
                        </NavLink>
                    )}
                </nav>
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
                    <div className="topbar-actions">
                        <NotificationBell />
                        <span className="role-badge">{roleLabels[user.role]}</span>
                    </div>
                </header>
                <main id="main" className="main-content">
                    <PageNavigation />
                    <Outlet />
                </main>
                <footer className="portal-footer">
                    Student Center<span>Student Services Portal</span>
                </footer>
            </div>
        </div>
    );
}
