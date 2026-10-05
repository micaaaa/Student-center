import { ArrowRight, UserRound, ShieldCheck, Mail, BookOpen } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { roleLabels } from '../components/Layout';

export function Dashboard() {
    const { user } = useAuth();
    if (!user) return null;
    const student = user.role === 'STUDENT';
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">OVERVIEW</span>
                <h1>Account overview · {user.username}</h1>
                <p className="muted">Account information and available services.</p>
            </div>
            <section className="welcome-card">
                <div>
                    <span className="eyebrow">STUDENT ADMINISTRATION</span>
                    <h2>{student ? 'Student profile' : 'Account details'}</h2>
                    <p>
                        {student
                            ? 'Review and update your personal, academic and contact information.'
                            : 'Review your account information and available services.'}
                    </p>
                    {student && (
                        <Link className="primary" to="/profile">
                            View profile <ArrowRight size={18} />
                        </Link>
                    )}
                </div>
                <div className="profile-illustration" aria-hidden="true">
                    <div className="illustrated-card">
                        <UserRound size={42} />
                        <i />
                        <i />
                        <span>STUDENT PROFILE</span>
                    </div>
                    <span className="illustration-stamp">
                        <ShieldCheck size={27} />
                    </span>
                </div>
            </section>
            <div className="section-heading">
                <h2>Account information</h2>
                <span className="status-pill">Active account</span>
            </div>
            <div className="account-grid">
                <article className="info-card">
                    <span className="tile-icon">
                        <UserRound />
                    </span>
                    <small>USERNAME</small>
                    <strong>{user.username}</strong>
                </article>
                <article className="info-card">
                    <span className="tile-icon">
                        <Mail />
                    </span>
                    <small>EMAIL ADDRESS</small>
                    <strong>{user.email}</strong>
                </article>
                <article className="info-card">
                    <span className="tile-icon">
                        <ShieldCheck />
                    </span>
                    <small>ROLE</small>
                    <strong>{roleLabels[user.role]}</strong>
                </article>
            </div>
            <div className="quiet-note">
                <BookOpen size={22} />
                <div>
                    <strong>{student ? 'Profile information' : 'Administrative services'}</strong>
                    <p>
                        {student
                            ? 'Academic and contact details can be updated on the profile page.'
                            : 'Select a service from the navigation menu to manage student records.'}
                    </p>
                </div>
            </div>
        </>
    );
}
