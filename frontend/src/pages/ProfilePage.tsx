import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import type { FormEvent } from 'react';
import { Check, Pencil, Save, UserRound } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { api, ApiError, errorMessage } from '../lib/api';
import type { Student } from '../lib/types';

const statuses: Record<string, string> = {
    ACTIVE: 'Active',
    INACTIVE: 'Inactive',
    GRADUATED: 'Graduated',
    SUSPENDED: 'Suspended',
};
const funding: Record<string, string> = { BUDGET: 'State-funded', SELFFINANCED: 'Self-funded' };

export function ProfilePage() {
    const { user } = useAuth();
    const [searchParams, setSearchParams] = useSearchParams();
    const completing = searchParams.get('complete') === 'true';
    const [profile, setProfile] = useState<Student | null>(null);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState('');
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [editing, setEditing] = useState(completing);
    const [busy, setBusy] = useState(false);
    const [attempt, setAttempt] = useState(0);

    useEffect(() => {
        const controller = new AbortController();
        setLoading(true);
        setLoadError('');
        api<Student>('/api/students/me', { signal: controller.signal })
            .then((student) => {
                if (!controller.signal.aborted) setProfile(student);
            })
            .catch((error) => {
                if (controller.signal.aborted) return;
                if (error instanceof ApiError && error.status === 404) setProfile(null);
                else setLoadError(errorMessage(error));
            })
            .finally(() => {
                if (!controller.signal.aborted) setLoading(false);
            });
        return () => controller.abort();
    }, [user?.id, attempt]);

    async function save(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        const value = (name: string) => String(form.get(name) ?? '').trim();
        const required = ['firstName', 'lastName', 'email', ...(!profile ? ['studentNumber'] : [])];
        if (required.some((name) => !value(name))) {
            setError('Complete all required fields. Values containing only spaces are not valid.');
            return;
        }
        const body = {
            firstName: value('firstName'),
            lastName: value('lastName'),
            email: value('email'),
            ...(!profile ? { studentNumber: value('studentNumber') } : {}),
            phone: value('phone') || null,
            faculty: value('faculty') || null,
            studyProgram: value('studyProgram') || null,
            studyLevel: value('studyLevel') || null,
            yearOfStudy: value('yearOfStudy') ? Number(value('yearOfStudy')) : null,
            fundingType: value('fundingType') || null,
            address: value('address') || null,
        };
        setBusy(true);
        setError('');
        setSuccess('');
        try {
            const saved = await api<Student>('/api/students/me', {
                method: profile ? 'PUT' : 'POST',
                body: JSON.stringify(body),
            });
            setSuccess(profile ? 'Changes saved.' : 'Profile created.');
            setProfile(saved);
            setEditing(false);
            setSearchParams({}, { replace: true });
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }

    if (loading)
        return (
            <div className="panel" role="status">
                Loading profile…
            </div>
        );
    if (loadError)
        return (
            <div className="panel">
                <h1>Profile unavailable</h1>
                <p role="alert">{loadError}</p>
                <button className="primary" onClick={() => setAttempt((value) => value + 1)}>
                    Try again
                </button>
            </div>
        );
    const formVisible = !profile || editing;

    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">STUDENT RECORD</span>
                    <h1>{completing ? 'Complete your profile' : 'My profile'}</h1>
                    <p className="muted">
                        {completing
                            ? 'Your account and student record are ready. Add your academic and contact details below.'
                            : 'Personal information and academic details.'}
                    </p>
                </div>
                {profile && !editing && (
                    <button
                        className="secondary"
                        onClick={() => {
                            setEditing(true);
                            setSuccess('');
                            setError('');
                        }}
                    >
                        <Pencil size={17} /> Edit profile
                    </button>
                )}
            </div>
            {success && (
                <div className="notice success" role="status">
                    <Check size={18} />
                    {success}
                </div>
            )}
            <div className="profile-layout">
                <section className="panel profile-panel">
                    <div className="panel-title">
                        <span className="tile-icon">
                            <UserRound />
                        </span>
                        <div>
                            <h2>{profile ? 'Personal information' : 'Create student profile'}</h2>
                            <p className="muted">
                                {profile
                                    ? 'Personal and academic information.'
                                    : 'Enter the required information to create your student record.'}
                            </p>
                        </div>
                    </div>
                    {formVisible ? (
                        <form onSubmit={save} key={profile?.id ?? 'new'}>
                            <fieldset disabled={busy}>
                                <div className="form-grid">
                                    <label>
                                        Student number {!profile && '*'}
                                        <input
                                            name="studentNumber"
                                            required
                                            maxLength={30}
                                            defaultValue={profile?.studentNumber}
                                            readOnly={!!profile}
                                            placeholder="e.g. RA 123/2026"
                                        />
                                    </label>
                                    <div className="form-hint">
                                        {profile
                                            ? 'The student number cannot be changed through this form.'
                                            : '* Required fields'}
                                    </div>
                                    <label>
                                        First name *
                                        <input
                                            name="firstName"
                                            required
                                            maxLength={100}
                                            autoComplete="given-name"
                                            defaultValue={profile?.firstName}
                                        />
                                    </label>
                                    <label>
                                        Last name *
                                        <input
                                            name="lastName"
                                            required
                                            maxLength={100}
                                            autoComplete="family-name"
                                            defaultValue={profile?.lastName}
                                        />
                                    </label>
                                    <label className="span-two">
                                        Contact email *
                                        <input
                                            name="email"
                                            type="email"
                                            required
                                            maxLength={256}
                                            autoComplete="email"
                                            defaultValue={profile?.email ?? user?.email}
                                        />
                                        <small>
                                            Changing the contact email does not change your sign-in
                                            email.
                                        </small>
                                    </label>
                                    <>
                                        <div className="form-divider span-two">
                                            <h3>Academic and contact information</h3>
                                            <p className="muted">
                                                These details are optional and can be updated later.
                                            </p>
                                        </div>
                                        <label className="span-two">
                                            Faculty
                                            <input
                                                name="faculty"
                                                maxLength={200}
                                                defaultValue={profile?.faculty ?? ''}
                                            />
                                        </label>
                                        <label>
                                            Study programme
                                            <input
                                                name="studyProgram"
                                                maxLength={200}
                                                defaultValue={profile?.studyProgram ?? ''}
                                            />
                                        </label>
                                        <label>
                                            Study level
                                            <input
                                                name="studyLevel"
                                                maxLength={100}
                                                placeholder="e.g. Undergraduate studies"
                                                defaultValue={profile?.studyLevel ?? ''}
                                            />
                                        </label>
                                        <label>
                                            Year of study
                                            <input
                                                name="yearOfStudy"
                                                type="number"
                                                min={1}
                                                max={10}
                                                step={1}
                                                defaultValue={profile?.yearOfStudy ?? ''}
                                            />
                                        </label>
                                        <label>
                                            Funding type
                                            <select
                                                name="fundingType"
                                                defaultValue={profile?.fundingType ?? ''}
                                            >
                                                <option value="">Not provided</option>
                                                <option value="BUDGET">State-funded</option>
                                                <option value="SELFFINANCED">Self-funded</option>
                                            </select>
                                        </label>
                                        <label className="span-two">
                                            Phone
                                            <input
                                                name="phone"
                                                type="tel"
                                                maxLength={30}
                                                autoComplete="tel"
                                                defaultValue={profile?.phone ?? ''}
                                            />
                                        </label>
                                        <label className="span-two">
                                            Address
                                            <input
                                                name="address"
                                                maxLength={500}
                                                autoComplete="street-address"
                                                defaultValue={profile?.address ?? ''}
                                            />
                                        </label>
                                    </>
                                </div>
                                {error && (
                                    <div className="notice error" role="alert">
                                        {error}
                                    </div>
                                )}
                                <div className="form-actions">
                                    {profile && (
                                        <button
                                            className="secondary"
                                            type="button"
                                            onClick={() => {
                                                setEditing(false);
                                                setSearchParams({}, { replace: true });
                                                setError('');
                                            }}
                                        >
                                            Cancel
                                        </button>
                                    )}
                                    <button className="primary" type="submit">
                                        <Save size={17} />
                                        {busy
                                            ? 'Saving…'
                                            : profile
                                              ? 'Save changes'
                                              : 'Create profile'}
                                    </button>
                                </div>
                            </fieldset>
                        </form>
                    ) : (
                        <dl className="details-grid">
                            {[
                                ['Full name', profile.firstName + ' ' + profile.lastName],
                                ['Student number', profile.studentNumber],
                                ['Contact email', profile.email],
                                ['Phone', profile.phone],
                                ['Faculty', profile.faculty],
                                ['Study programme', profile.studyProgram],
                                ['Study level', profile.studyLevel],
                                ['Year of study', profile.yearOfStudy],
                                ['Funding', funding[profile.fundingType ?? '']],
                                ['Address', profile.address],
                            ].map(([label, value]) => (
                                <div key={String(label)}>
                                    <dt>{label}</dt>
                                    <dd>
                                        {value || <span className="empty-value">Not provided</span>}
                                    </dd>
                                </div>
                            ))}
                        </dl>
                    )}
                </section>
                <aside className="profile-aside">
                    <div className="panel">
                        <span className="eyebrow">STUDENT STATUS</span>
                        <span className="status-pill">
                            {profile
                                ? (statuses[profile.status] ?? profile.status)
                                : 'Profile not created'}
                        </span>
                        <p className="muted">
                            {profile
                                ? 'Student status is managed by the Student Center. Contact student administration to amend restricted information.'
                                : 'Create a profile to link your account to a student record.'}
                        </p>
                    </div>
                    <div className="profile-tip">
                        <h3>Data accuracy</h3>
                        <p>
                            Verify your student number and contact details before submitting the
                            form.
                        </p>
                    </div>
                </aside>
            </div>
        </>
    );
}
