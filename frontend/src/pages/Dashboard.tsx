import { StaffServiceSummary } from '../components/StaffServiceSummary';
import { StudentServiceSummary } from '../components/StudentServiceSummary';
import { useState } from 'react';
import { ArrowRight, RefreshCw, UserRound } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { portalServices } from '../lib/navigation';

export function Dashboard() {
    const { user } = useAuth();
    const [version, setVersion] = useState(0);
    if (!user) return null;
    const services = portalServices(user);
    if (user.role === 'STUDENT') {
        services.push({
            id: 'profile',
            title: 'My profile',
            description: 'Review and update your personal, academic and contact details.',
            icon: UserRound,
            links: [{ label: 'Open my profile', to: '/profile' }],
        });
    }
    return (
        <div className="home-page">
            <div className="page-heading home-heading">
                <div>
                    <span className="eyebrow">STUDENT CENTER</span>
                    <h1>
                        {user.role === 'STUDENT' ? 'Student services' : 'Service administration'}
                    </h1>
                    <p className="muted">Choose a service to get started.</p>
                </div>
                <button className="secondary" onClick={() => setVersion((value) => value + 1)}>
                    <RefreshCw size={15} aria-hidden="true" /> Refresh overview
                </button>
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
            <div className="service-grid">
                {services.map(({ id, title, description, icon: Icon, links }) => (
                    <section className="service-card" key={id} aria-labelledby={'service-' + id}>
                        <div className="service-card-heading">
                            <span className="service-icon-wrap">
                                <Icon className="service-icon" size={20} aria-hidden="true" />
                            </span>
                            <h2 id={'service-' + id}>{title}</h2>
                        </div>
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
                                    className={index === 0 ? 'primary' : 'secondary'}
                                >
                                    {link.label}
                                    {index === 0 && <ArrowRight size={17} aria-hidden="true" />}
                                </Link>
                            ))}
                        </div>
                    </section>
                ))}
            </div>
        </div>
    );
}
