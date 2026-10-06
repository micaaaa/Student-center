import { useState } from 'react';
import type { FormEvent } from 'react';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import type { MaintenanceCategory } from '../lib/maintenance';
import { RequestError, StatusBadge } from '../components/ApplicationUi';

export function MaintenanceCategoriesPage() {
    const resource = useResource<MaintenanceCategory[]>('/api/maintenance/categories/management');
    const [editing, setEditing] = useState<MaintenanceCategory | 'new' | null>(null);
    return (
        <>
            <div className="page-heading heading-row">
                <h1>Maintenance categories</h1>
                <button className="primary" disabled={!!editing} onClick={() => setEditing('new')}>
                    Add category
                </button>
            </div>
            {editing && (
                <CategoryForm
                    key={editing === 'new' ? 'new' : editing.id}
                    item={editing === 'new' ? undefined : editing}
                    cancel={() => setEditing(null)}
                    saved={() => {
                        setEditing(null);
                        resource.reload();
                    }}
                />
            )}
            {resource.loading ? (
                <p role="status">Loading categories…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !resource.data?.length ? (
                <p className="panel">No maintenance categories found.</p>
            ) : (
                <div className="competition-list application-section">
                    {resource.data.map((item) => (
                        <article className="panel" key={item.id}>
                            <h2>{item.name}</h2>
                            <StatusBadge status={item.isActive ? 'ACTIVE' : 'INACTIVE'} />
                            <p className="preserve-lines">
                                {item.description || 'No description provided.'}
                            </p>
                            <button
                                className="secondary"
                                disabled={!!editing}
                                onClick={() => setEditing(item)}
                            >
                                Edit {item.name}
                            </button>
                        </article>
                    ))}
                </div>
            )}
        </>
    );
}
function CategoryForm({
    item,
    saved,
    cancel,
}: {
    item?: MaintenanceCategory;
    saved: () => void;
    cancel: () => void;
}) {
    const [name, setName] = useState(item?.name || '');
    const [description, setDescription] = useState(item?.description || '');
    const [active, setActive] = useState(item?.isActive ?? true);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (busy || !name.trim()) return;
        setBusy(true);
        setError('');
        try {
            await api('/api/maintenance/categories' + (item ? '/' + item.id : ''), {
                method: item ? 'PUT' : 'POST',
                body: JSON.stringify({
                    name: name.trim(),
                    description: description.trim() || null,
                    isActive: active,
                }),
            });
            saved();
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <section className="panel">
            <h2>{item ? 'Edit category' : 'Add category'}</h2>
            <form onSubmit={submit}>
                {error && (
                    <p className="notice error" role="alert">
                        {error}
                    </p>
                )}
                <fieldset className="competition-fields" disabled={busy}>
                    <label>
                        Category name
                        <input
                            required
                            maxLength={100}
                            value={name}
                            onChange={(e) => setName(e.target.value)}
                        />
                    </label>
                    <label>
                        Description
                        <textarea
                            rows={3}
                            maxLength={1000}
                            value={description}
                            onChange={(e) => setDescription(e.target.value)}
                        />
                    </label>
                    <label>
                        Status
                        <select
                            value={String(active)}
                            onChange={(e) => setActive(e.target.value === 'true')}
                        >
                            <option value="true">Active</option>
                            <option value="false">Inactive</option>
                        </select>
                    </label>
                    <p className="muted">Inactive categories cannot be used for new requests.</p>
                    <div className="button-row">
                        <button className="primary" disabled={!name.trim()}>
                            Save category
                        </button>
                        <button type="button" className="secondary" onClick={cancel}>
                            Cancel
                        </button>
                    </div>
                </fieldset>
            </form>
        </section>
    );
}
