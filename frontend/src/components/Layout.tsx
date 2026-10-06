import { useState } from 'react';
import {
    GraduationCap,
    LayoutDashboard,
    UserRound,
    LogOut,
    ArrowUpRight,
    Menu,
    X,
    CalendarDays,
    Files,
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
                    {['STAFF', 'ADMIN'].includes(user.role) &&
                        user.permissions.includes('ManageMaintenance') && (
                            <NavLink to="/staff/maintenance" onClick={() => setOpen(false)}>
                                <Files size={19} /> Maintenance requests
                            </NavLink>
                        )}
                    {['STAFF', 'ADMIN'].includes(user.role) &&
                        user.permissions.includes('ManageFood') && (
                            <>
                                <NavLink to="/staff/restaurants" onClick={() => setOpen(false)}>
                                    <CalendarDays size={19} /> Restaurants and menus
                                </NavLink>
                                <NavLink to="/staff/meals" onClick={() => setOpen(false)}>
                                    <Files size={19} /> Meal administration
                                </NavLink>
                            </>
                        )}
                    {['STAFF', 'ADMIN'].includes(user.role) &&
                        user.permissions.includes('ManageAccommodation') && (
                            <>
                                <NavLink to="/staff/dorms" onClick={() => setOpen(false)}>
                                    <GraduationCap size={19} /> Dormitories and rooms
                                </NavLink>
                                <NavLink to="/staff/assignments" onClick={() => setOpen(false)}>
                                    <UserRound size={19} /> Room assignments
                                </NavLink>
                            </>
                        )}
                    {['STAFF', 'ADMIN'].includes(user.role) &&
                        user.permissions.includes('ManageApplications') && (
                            <>
                                <NavLink to="/staff/competitions" onClick={() => setOpen(false)}>
                                    <CalendarDays size={19} /> Competitions
                                </NavLink>
                                <NavLink to="/staff/applications" onClick={() => setOpen(false)}>
                                    <Files size={19} /> Application review
                                </NavLink>
                                <NavLink to="/staff/rankings" onClick={() => setOpen(false)}>
                                    <CalendarDays size={19} /> Rankings and appeals
                                </NavLink>
                            </>
                        )}
                    <NavLink to="/" end onClick={() => setOpen(false)}>
                        <LayoutDashboard size={19} /> Overview
                    </NavLink>
                    {user.role === 'STUDENT' && (
                        <>
                            <NavLink to="/profile" onClick={() => setOpen(false)}>
                                <UserRound size={19} /> My profile
                            </NavLink>
                            <NavLink to="/maintenance" onClick={() => setOpen(false)}>
                                <Files size={19} /> Maintenance requests
                            </NavLink>
                            <NavLink to="/my-meals" onClick={() => setOpen(false)}>
                                <Files size={19} /> My meals
                            </NavLink>
                            <NavLink to="/restaurants" onClick={() => setOpen(false)}>
                                <CalendarDays size={19} /> Restaurants and menus
                            </NavLink>
                            <NavLink to="/my-accommodation" onClick={() => setOpen(false)}>
                                <GraduationCap size={19} /> My accommodation
                            </NavLink>
                            <NavLink to="/competitions" onClick={() => setOpen(false)}>
                                <CalendarDays size={19} /> Competitions
                            </NavLink>
                            <NavLink to="/applications" onClick={() => setOpen(false)}>
                                <Files size={19} /> My applications
                            </NavLink>
                        </>
                    )}
                </nav>
                <div className="sidebar-note">
                    <h3>Account services</h3>
                    <p>
                        {user.role === 'STUDENT'
                            ? 'Manage your personal and academic information.'
                            : 'Review your account details and available services.'}
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
