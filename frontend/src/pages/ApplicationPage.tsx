import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Download, FileText, Save, Send, Trash2, Upload } from 'lucide-react';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { acceptsApplications, dateTime, documentTypes, useNow } from '../lib/applications';
import type { ApplicationDocument, Competition, StudentApplication } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { ConfirmationDialog } from '../components/ConfirmationDialog';

export function ApplicationPage() {
    const { id } = useParams();
    const resource = useResource<StudentApplication>('/api/applications/' + id);
    if (resource.loading) return <p role="status">Loading application…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    return (
        <ApplicationRecord
            key={resource.data!.id}
            application={resource.data!}
            update={resource.setData}
            reload={resource.reload}
        />
    );
}

function ApplicationRecord({
    application,
    update,
    reload,
}: {
    application: StudentApplication;
    update: (application: StudentApplication) => void;
    reload: () => void;
}) {
    const competition = useResource<Competition>('/api/competitions/' + application.competitionId);
    const documents = useResource<ApplicationDocument[]>(
        '/api/applications/' + application.id + '/documents',
    );
    const [note, setNote] = useState(application.note ?? '');
    const [busy, setBusy] = useState('');
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const [confirmation, setConfirmation] = useState<'submit' | ApplicationDocument | null>(null);
    const now = useNow();
    const draft = application.status === 'DRAFT';
    const dirty = note.trim() !== (application.note ?? '').trim();
    const accepting = !!competition.data && acceptsApplications(competition.data, now);
    const canSubmit = draft && accepting && !dirty && !documents.loading && !documents.error;
    const basePath = '/api/applications/' + application.id;

    function start(action: string) {
        setBusy(action);
        setError('');
        setSuccess('');
    }

    async function save(event: FormEvent) {
        event.preventDefault();
        start('save');
        try {
            const saved = await api<StudentApplication>(basePath, {
                method: 'PUT',
                body: JSON.stringify({ note: note.trim() || null }),
            });
            update(saved);
            setNote(saved.note ?? '');
            setSuccess('Application changes saved.');
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy('');
        }
    }

    async function upload(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const form = event.currentTarget;
        const body = new FormData(form);
        const file = body.get('file');
        setError('');
        setSuccess('');
        if (!(file instanceof File) || file.size === 0) {
            setError('Select a non-empty file.');
            return;
        }
        if (file.size > 10 * 1024 * 1024) {
            setError('The maximum file size is 10 MB.');
            return;
        }
        if (!/\.(pdf|jpe?g|png)$/i.test(file.name)) {
            setError('Allowed file formats are PDF, JPG and PNG.');
            return;
        }
        if (file.name.length > 255 || /[\u0000-\u001f\u007f]/.test(file.name)) {
            setError('The file name must not exceed 255 characters or contain control characters.');
            return;
        }
        start('upload');
        try {
            const saved = await api<ApplicationDocument>(basePath + '/documents', {
                method: 'POST',
                body,
            });
            documents.setData([...(documents.data ?? []), saved]);
            form.reset();
            setSuccess('Document uploaded.');
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy('');
        }
    }

    async function download(document: ApplicationDocument) {
        start('download-' + document.id);
        try {
            const file = await api<Blob>(basePath + '/documents/' + document.id + '/download', {
                responseType: 'blob',
            });
            const url = URL.createObjectURL(file);
            const link = window.document.createElement('a');
            link.href = url;
            link.download = document.fileName;
            window.document.body.appendChild(link);
            link.click();
            link.remove();
            window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy('');
        }
    }

    async function confirm() {
        if (!confirmation) return;
        const action = confirmation;
        if (action === 'submit' && !canSubmit) {
            setConfirmation(null);
            setError(
                'The application cannot be submitted. Check the deadline and save any changes.',
            );
            return;
        }
        start(action === 'submit' ? 'submit' : 'delete');
        try {
            if (action === 'submit') {
                const saved = await api<StudentApplication>(basePath + '/submit', {
                    method: 'POST',
                });
                update(saved);
                setSuccess('Application submitted. Further changes are no longer permitted.');
            } else {
                await api<void>(basePath + '/documents/' + action.id, { method: 'DELETE' });
                documents.setData(
                    (documents.data ?? []).filter((document) => document.id !== action.id),
                );
                setSuccess('Document removed.');
            }
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy('');
            setConfirmation(null);
        }
    }

    return (
        <>
            <Link className="back-link" to="/applications">
                <ArrowLeft size={16} />
                My applications
            </Link>
            <div className="page-heading heading-row">
                <div>
                    <span className="eyebrow">APPLICATION RECORD</span>
                    <h1>{competition.data?.name ?? 'Application details'}</h1>
                    <p className="muted record-reference">Reference: {application.id}</p>
                    <Link className="text-link" to={'/applications/' + application.id + '/results'}>
                        View score, decision and appeal
                    </Link>
                </div>
                <StatusBadge status={application.status} />
            </div>
            {error && (
                <div className="notice error" role="alert">
                    <p>{error}</p>
                    <button className="secondary" disabled={!!busy} onClick={reload}>
                        Reload application
                    </button>
                </div>
            )}
            {success && (
                <div className="notice success" role="status">
                    {success}
                </div>
            )}
            {!!competition.error && (
                <RequestError error={competition.error} retry={competition.reload} />
            )}
            <section className="panel application-summary">
                <dl className="details-grid">
                    <div>
                        <dt>Created</dt>
                        <dd>{dateTime(application.createdAtUtc)}</dd>
                    </div>
                    <div>
                        <dt>Submitted</dt>
                        <dd>{dateTime(application.submittedAtUtc)}</dd>
                    </div>
                    <div>
                        <dt>Academic year</dt>
                        <dd>{competition.data?.academicYear ?? 'Unavailable'}</dd>
                    </div>
                    <div>
                        <dt>Application deadline</dt>
                        <dd>
                            {competition.data
                                ? dateTime(competition.data.applicationEndDateUtc)
                                : 'Unavailable'}
                        </dd>
                    </div>
                </dl>
                <Link className="text-link" to={'/competitions/' + application.competitionId}>
                    View competition details
                </Link>
            </section>
            <section className="panel application-section">
                <div className="panel-title">
                    <FileText size={22} />
                    <div>
                        <h2>Application information</h2>
                        <p className="muted">
                            {draft
                                ? 'Draft changes must be saved before submission.'
                                : 'This application is read-only.'}
                        </p>
                    </div>
                </div>
                {draft ? (
                    <form onSubmit={save}>
                        <fieldset disabled={!!busy}>
                            <label>
                                Additional note (optional)
                                <textarea
                                    name="note"
                                    rows={5}
                                    value={note}
                                    onChange={(event) => setNote(event.target.value)}
                                    placeholder="Additional information relevant to your application"
                                />
                            </label>
                            <div className="form-actions">
                                {dirty && <span className="muted">Unsaved changes</span>}
                                <button className="primary" disabled={!dirty} type="submit">
                                    <Save size={17} />
                                    {busy === 'save' ? 'Saving…' : 'Save changes'}
                                </button>
                            </div>
                        </fieldset>
                    </form>
                ) : (
                    <p className="preserve-lines">
                        {application.note || 'No additional note provided.'}
                    </p>
                )}
            </section>
            <section className="panel application-section">
                <div className="panel-title">
                    <Upload size={22} />
                    <div>
                        <h2>Supporting documents</h2>
                        <p className="muted">
                            Accepted formats: PDF, JPG and PNG. Maximum file size: 10 MB.
                        </p>
                    </div>
                </div>
                {documents.loading ? (
                    <p role="status">Loading documents…</p>
                ) : documents.error ? (
                    <RequestError error={documents.error} retry={documents.reload} />
                ) : (
                    <>
                        {documents.data!.length === 0 ? (
                            <p className="muted">No documents have been uploaded.</p>
                        ) : (
                            <ul className="document-list">
                                {documents.data!.map((document) => (
                                    <li key={document.id}>
                                        <div className="document-info">
                                            <strong>{document.fileName}</strong>
                                            <span>
                                                {documentTypes.find(
                                                    (type) => type.name === document.documentType,
                                                )?.label ?? document.documentType}{' '}
                                                · {(document.size / 1024).toFixed(1)} KB
                                            </span>
                                            <span>Uploaded {dateTime(document.uploadedAtUtc)}</span>
                                            <StatusBadge status={document.status} />
                                            {document.reviewComment && (
                                                <p className="review-comment">
                                                    Review comment: {document.reviewComment}
                                                </p>
                                            )}
                                        </div>
                                        <div className="document-actions">
                                            <button
                                                className="secondary"
                                                disabled={!!busy}
                                                onClick={() => download(document)}
                                                aria-label={'Download ' + document.fileName}
                                            >
                                                <Download size={16} />
                                                Download
                                            </button>
                                            {draft && (
                                                <button
                                                    className="secondary danger-text"
                                                    disabled={!!busy}
                                                    onClick={() => setConfirmation(document)}
                                                    aria-label={'Remove ' + document.fileName}
                                                >
                                                    <Trash2 size={16} />
                                                    Remove
                                                </button>
                                            )}
                                        </div>
                                    </li>
                                ))}
                            </ul>
                        )}
                        {draft && (
                            <form onSubmit={upload} className="upload-form">
                                <fieldset disabled={!!busy}>
                                    <div className="form-grid">
                                        <label>
                                            Document category
                                            <select name="documentType" required defaultValue="1">
                                                {documentTypes.map((type) => (
                                                    <option key={type.value} value={type.value}>
                                                        {type.label}
                                                    </option>
                                                ))}
                                            </select>
                                        </label>
                                        <label>
                                            Document file
                                            <input
                                                name="file"
                                                type="file"
                                                accept=".pdf,.jpg,.jpeg,.png"
                                                required
                                            />
                                        </label>
                                    </div>
                                    <button className="secondary" type="submit">
                                        <Upload size={17} />
                                        {busy === 'upload' ? 'Uploading…' : 'Upload document'}
                                    </button>
                                </fieldset>
                            </form>
                        )}
                    </>
                )}
            </section>
            {draft && (
                <section className="panel submission-panel">
                    <div>
                        <h2>Submit application</h2>
                        <p className="muted">
                            Review your information and the competition requirements before
                            submitting. Submitted applications cannot be edited.
                        </p>
                        {!accepting && !competition.loading && (
                            <p className="notice">
                                Submission is unavailable outside the application period or when the
                                competition is not open.
                            </p>
                        )}
                        {dirty && <p className="notice">Save your changes before submitting.</p>}
                    </div>
                    <button
                        className="primary"
                        disabled={!!busy || !canSubmit}
                        onClick={() => setConfirmation('submit')}
                    >
                        <Send size={17} />
                        Submit application
                    </button>
                </section>
            )}
            {confirmation && (
                <ConfirmationDialog
                    title={
                        confirmation === 'submit' ? 'Submit this application?' : 'Remove document?'
                    }
                    busy={!!busy}
                    onConfirm={confirm}
                    onClose={() => setConfirmation(null)}
                >
                    <p>
                        {confirmation === 'submit'
                            ? 'Once submitted, the application and its documents can no longer be changed. Confirm that the information is complete.'
                            : 'The document "' +
                              confirmation.fileName +
                              '" will be removed from this draft application.'}
                    </p>
                </ConfirmationDialog>
            )}
        </>
    );
}
