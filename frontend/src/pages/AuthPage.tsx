import { useState } from 'react';
import type { FormEvent } from 'react';
import { ArrowRight, BookOpen, Eye, EyeOff, GraduationCap, ShieldCheck } from 'lucide-react';
import { Link, Navigate, useLocation } from 'react-router';
import { authenticate, errorMessage } from '../lib/api';
import { useAuth } from '../auth/AuthContext';

export function AuthPage({ register = false }: { register?: boolean }) {
    const { user, loading } = useAuth();
    const location = useLocation();
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [visible, setVisible] = useState(false);
    const target = location.state?.from;
    const destination =
        typeof target === 'string' && target.startsWith('/') && !target.startsWith('//')
            ? target
            : '/';
    if (!loading && user)
        return <Navigate to={register ? '/profile?complete=true' : destination} replace />;

    async function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        const password = String(form.get('password'));
        if (register && password !== form.get('confirmation')) {
            setError('Passwords do not match.');
            return;
        }
        if (
            register &&
            ['username', 'firstName', 'lastName', 'studentNumber'].some(
                (field) => !String(form.get(field) ?? '').trim(),
            )
        ) {
            setError('Complete all required fields. Values containing only spaces are not valid.');
            return;
        }
        setBusy(true);
        setError('');
        try {
            await authenticate(register ? 'register' : 'login', {
                email: String(form.get('email')).trim(),
                password,
                ...(register
                    ? {
                          username: String(form.get('username')).trim(),
                          firstName: String(form.get('firstName')).trim(),
                          lastName: String(form.get('lastName')).trim(),
                          studentNumber: String(form.get('studentNumber')).trim().toUpperCase(),
                      }
                    : {}),
            });
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }

    return (
        <main className="auth-page">
            <section className="auth-story">
                <Link className="brand" to="/">
                    <span className="brand-icon">
                        <GraduationCap />
                    </span>
                    <span>
                        Student Center<small>STUDENT SERVICES PORTAL</small>
                    </span>
                </Link>
                <div className="story-copy">
                    <span className="eyebrow light">STUDENT CENTER</span>
                    <h1>
                        Student services
                        <br />
                        <em>Online portal</em>
                    </h1>
                    <p>Access your account and manage your student information.</p>
                    <div className="campus-art" aria-hidden="true">
                        <div className="sun" />
                        <div className="building building-one">
                            <i />
                            <i />
                            <i />
                            <i />
                            <i />
                            <i />
                        </div>
                        <div className="building building-two">
                            <i />
                            <i />
                            <i />
                            <i />
                            <i />
                            <i />
                            <i />
                            <i />
                            <i />
                        </div>
                        <div className="tree" />
                        <div className="ground" />
                    </div>
                    <div className="story-caption">
                        <BookOpen size={18} />
                        <span>Accommodation, dining and student administration</span>
                    </div>
                </div>
                <span className="story-footer">Student Center · Online services</span>
            </section>
            <section className="auth-panel">
                <div className="auth-switch">
                    {register ? 'Already registered?' : 'New user?'}{' '}
                    <Link to={register ? '/login' : '/register'}>
                        {register ? 'Sign in' : 'Create account'} <ArrowRight size={15} />
                    </Link>
                </div>
                <div className="auth-form-wrap">
                    <span className="eyebrow">ACCOUNT ACCESS</span>
                    <h2>{register ? 'Student registration' : 'Sign in'}</h2>
                    <p className="muted">
                        {register
                            ? 'Create your account and student record. You can add academic and contact details next.'
                            : 'Enter your credentials to access the portal.'}
                    </p>
                    <form onSubmit={submit}>
                        <fieldset disabled={busy || loading}>
                            {register && (
                                <label>
                                    Username
                                    <input
                                        name="username"
                                        autoComplete="username"
                                        required
                                        minLength={3}
                                        maxLength={50}
                                        pattern="[a-zA-Z0-9._-]+"
                                        placeholder="e.g. ana.petrovic"
                                    />
                                    <small>
                                        Use letters A–Z, numbers, periods, hyphens or underscores.
                                    </small>
                                </label>
                            )}
                            {register && (
                                <>
                                    <div className="auth-name-fields">
                                        <label>
                                            First name
                                            <input
                                                name="firstName"
                                                autoComplete="given-name"
                                                required
                                                maxLength={100}
                                            />
                                        </label>
                                        <label>
                                            Last name
                                            <input
                                                name="lastName"
                                                autoComplete="family-name"
                                                required
                                                maxLength={100}
                                            />
                                        </label>
                                    </div>
                                    <label>
                                        Student number
                                        <input
                                            name="studentNumber"
                                            required
                                            maxLength={30}
                                            placeholder="e.g. RA 123/2026"
                                        />
                                        <small>
                                            Enter your unique student number. It cannot be changed
                                            through your profile.
                                        </small>
                                    </label>
                                </>
                            )}
                            <label>
                                Email address
                                <input
                                    name="email"
                                    type="email"
                                    autoComplete="email"
                                    required
                                    maxLength={256}
                                    placeholder="name@example.com"
                                />
                            </label>
                            <label>
                                Password
                                <div className="password-field">
                                    <input
                                        name="password"
                                        type={visible ? 'text' : 'password'}
                                        autoComplete={
                                            register ? 'new-password' : 'current-password'
                                        }
                                        required
                                        minLength={8}
                                        maxLength={128}
                                        placeholder={
                                            register
                                                ? 'At least 8 characters'
                                                : 'Enter your password'
                                        }
                                    />
                                    <button
                                        type="button"
                                        className="icon-button"
                                        aria-label={visible ? 'Hide password' : 'Show password'}
                                        onClick={() => setVisible(!visible)}
                                    >
                                        {visible ? <EyeOff size={18} /> : <Eye size={18} />}
                                    </button>
                                </div>
                            </label>
                            {register && (
                                <label>
                                    Confirm password
                                    <input
                                        name="confirmation"
                                        type={visible ? 'text' : 'password'}
                                        autoComplete="new-password"
                                        required
                                        minLength={8}
                                        maxLength={128}
                                        placeholder="Re-enter your password"
                                    />
                                </label>
                            )}
                            {error && (
                                <div className="notice error" role="alert">
                                    {error}
                                </div>
                            )}
                            {location.state?.notice && (
                                <div className="notice" role="status">
                                    {location.state.notice}
                                </div>
                            )}
                            <button className="primary full" type="submit">
                                {busy ? 'Processing…' : register ? 'Create account' : 'Sign in'}
                                <ArrowRight size={18} />
                            </button>
                        </fieldset>
                    </form>
                    <p className="secure-note">
                        <ShieldCheck size={17} /> Access is restricted to authorized users.
                    </p>
                </div>
                <footer className="auth-footer">
                    Student Center <span>Student Services Portal</span>
                </footer>
            </section>
        </main>
    );
}
