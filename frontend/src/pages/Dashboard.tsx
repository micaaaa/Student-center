import { StaffServiceSummary } from '../components/StaffServiceSummary';
import { StudentServiceSummary } from '../components/StudentServiceSummary';
import { useState } from 'react';
import { ArrowRight, UserRound } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { portalServices } from '../lib/navigation';

export function Dashboard() {
    const { user } = useAuth();
    const [version, setVersion] = useState(0);
    if (!user) return null;
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">STUDENT CENTER</span>
                <h1>{user.role === 'STUDENT' ? 'Student services' : 'Service administration'}</h1>
                <p className="muted">Choose a service to get started.</p>
            </div>
            {user.role === 'STUDENT' && (
                <div className="home-profile">
                    <UserRound size={22} aria-hidden="true" />
                    <div>
                        <strong>Applying for accommodation?</strong>
                        <p>
                            Check your personal and academic details before submitting an
                            application.
                        </p>
                    </div>
                    <Link className="text-link" to="/profile">
                        Review my profile <ArrowRight size={16} />
                    </Link>
                </div>
            )}
            <div className="button-row application-section">
                <button className="secondary" onClick={() => setVersion((value) => value + 1)}>
                    Refresh overview
                </button>
            </div>
            <div className="service-grid">
                {portalServices(user).map(({ id, title, description, icon: Icon, links }) => (
                    <section className="service-card" key={id} aria-labelledby={'service-' + id}>
                        <Icon className="service-icon" size={26} aria-hidden="true" />
                        <h2 id={'service-' + id}>{title}</h2>
                        <p>{description}</p>
                        {user.role === 'STUDENT' && (
                            <StudentServiceSummary key={version} service={id} />
                        )}
                        {user.role !== 'STUDENT' && (
                            <StaffServiceSummary
                                key={version}
                                service={id}
                                permissions={user.permissions}
                            />
                        )}
                        <div className="service-actions">
                            {links.map((link, index) => (
                                <Link
                                    key={link.to}
                                    to={link.to}
                                    className={index === 0 ? 'service-primary' : 'text-link'}
                                >
                                    {link.label}
                                    {index === 0 && <ArrowRight size={17} aria-hidden="true" />}
                                </Link>
                            ))}
                        </div>
                    </section>
                ))}
            </div>
        </>
    );
}
