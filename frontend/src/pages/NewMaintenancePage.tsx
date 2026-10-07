import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { useResource } from '../hooks/useResource';
import { useOptionalResource } from '../lib/results';
import { api, errorMessage } from '../lib/api';
import type { MyAccommodation } from '../lib/accommodation';
import type { MaintenanceCategory, MaintenanceRequest } from '../lib/maintenance';
import { priorities } from '../lib/maintenance';
import { RequestError } from '../components/ApplicationUi';

export function NewMaintenancePage() {
    const accommodation = useOptionalResource<MyAccommodation>('/api/accommodations/me', [
        'You do not have a current accommodation.',
    ]);
    const categories = useResource<MaintenanceCategory[]>('/api/maintenance/categories');
    const [categoryId, setCategoryId] = useState('');
    const [title, setTitle] = useState('');
    const [description, setDescription] = useState('');
    const [priority, setPriority] = useState(2);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const navigate = useNavigate();
    const activeCategories = categories.data?.filter((category) => category.isActive) || [];
    const ready =
        !accommodation.loading &&
        !accommodation.error &&
        accommodation.data?.status === 'ACTIVE' &&
        !categories.loading &&
        !categories.error;
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (
            !ready ||
            busy ||
            !title.trim() ||
            !description.trim() ||
            !categories.data?.some((c) => c.id === categoryId && c.isActive)
        )
            return;
        setBusy(true);
        setError('');
        try {
            const saved = await api<MaintenanceRequest>('/api/maintenance/requests/me', {
                method: 'POST',
                body: JSON.stringify({
                    categoryId,
                    title: title.trim(),
                    description: description.trim(),
                    priority,
                }),
            });
            navigate('/maintenance/' + saved.id, { replace: true });
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <>
            <div className="page-heading">
                <h1>Report a problem</h1>
                <p className="muted">The request will be linked to your current room.</p>
                <button
                    className="secondary"
                    disabled={accommodation.loading || categories.loading}
                    onClick={() => {
                        accommodation.reload();
                        categories.reload();
                    }}
                >
                    Refresh availability
                </button>
            </div>
            {accommodation.loading ? (
                <p role="status">Checking accommodation…</p>
            ) : accommodation.error ? (
                <RequestError error={accommodation.error} retry={accommodation.reload} />
            ) : accommodation.data?.status !== 'ACTIVE' ? (
                <section className="panel">
                    <h2>
                        {accommodation.data?.status === 'ASSIGNED'
                            ? 'Your room is reserved'
                            : 'No active accommodation'}
                    </h2>
                    <p>
                        {accommodation.data?.status === 'ASSIGNED'
                            ? `You have been assigned room ${accommodation.data.room.number} in ${accommodation.data.dorm.name}. Ask the accommodation office to record your move-in. You can report a room problem after this step.`
                            : 'Room problems can be reported after a room has been assigned and the accommodation office has recorded your move-in.'}
                    </p>
                    <Link className="secondary" to="/my-accommodation">
                        View my accommodation
                    </Link>
                </section>
            ) : (
                <p className="panel">
                    {accommodation.data.dorm.name} · Room {accommodation.data.room.number}
                </p>
            )}
            {accommodation.data?.status === 'ACTIVE' &&
                !accommodation.loading &&
                !accommodation.error &&
                (categories.loading ? (
                    <p role="status">Loading categories…</p>
                ) : categories.error ? (
                    <RequestError error={categories.error} retry={categories.reload} />
                ) : !activeCategories.length ? (
                    <section className="panel">
                        <h2>Problem reporting is not available yet</h2>
                        <p>
                            The maintenance office needs to activate a problem category before you
                            can submit a request. Please contact the office, then refresh
                            availability.
                        </p>
                    </section>
                ) : null)}
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            {ready && !!activeCategories.length && (
                <section className="panel">
                    <form onSubmit={submit}>
                        <fieldset className="competition-fields" disabled={busy}>
                            <label>
                                Category
                                <select
                                    required
                                    value={categoryId}
                                    onChange={(e) => setCategoryId(e.target.value)}
                                >
                                    <option value="">Select a category</option>
                                    {activeCategories.map((c) => (
                                        <option key={c.id} value={c.id}>
                                            {c.name}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            <label>
                                Title
                                <input
                                    required
                                    maxLength={200}
                                    value={title}
                                    onChange={(e) => setTitle(e.target.value)}
                                />
                            </label>
                            <label>
                                Description
                                <textarea
                                    required
                                    rows={6}
                                    maxLength={4000}
                                    value={description}
                                    onChange={(e) => setDescription(e.target.value)}
                                />
                            </label>
                            <label>
                                Priority
                                <select
                                    value={priority}
                                    onChange={(e) => setPriority(Number(e.target.value))}
                                >
                                    {priorities.map((p, i) => (
                                        <option key={p} value={i + 1}>
                                            {p}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            <button
                                className="primary application-section"
                                disabled={!title.trim() || !description.trim() || !categoryId}
                            >
                                {busy ? 'Submitting…' : 'Submit request'}
                            </button>
                        </fieldset>
                    </form>
                </section>
            )}
        </>
    );
}
