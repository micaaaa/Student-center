import { useState } from 'react';
import type { FormEvent } from 'react';
import { useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { dateTime, documentTypes } from '../lib/applications';
import type { ApplicationDocument, Competition, StudentApplication } from '../lib/applications';
import { useOptionalResource } from '../lib/results';
import type { Score } from '../lib/results';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { StaffScoreForm } from '../components/StaffScoreForm';

export function StaffApplicationPage() {
    const { id } = useParams();
    const application = useResource<StudentApplication>('/api/staff/applications/' + id);
    if (application.loading) return <p role="status">Loading application…</p>;
    if (application.error)
        return <RequestError error={application.error} retry={application.reload} />;
    return (
        <ReviewRecord
            key={id}
            application={application.data!}
            update={application.setData}
            reload={application.reload}
        />
    );
}

function ReviewRecord({
    application,
    update,
    reload,
}: {
    application: StudentApplication;
    update: (value: StudentApplication) => void;
    reload: () => void;
}) {
    const base = '/api/staff/applications/' + application.id;
    const documents = useResource<ApplicationDocument[]>(base + '/documents');
    const score = useOptionalResource<Score>(base + '/score', [
        'The application has not been scored yet.',
    ]);
    const competition = useResource<Competition>('/api/competitions/' + application.competitionId);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const reviewing = application.status === 'UNDER_REVIEW';
    const allValid =
        !documents.loading &&
        !documents.error &&
        !!documents.data?.length &&
        documents.data.every((document) => document.status === 'VALID');

    async function action(work: () => Promise<void>, message: string) {
        if (busy) return;
        setBusy(true);
        setError('');
        setSuccess('');
        try {
            await work();
            setSuccess(message);
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }

    async function download(document: ApplicationDocument) {
        await action(async () => {
            const file = await api<Blob>(`${base}/documents/${document.id}/download`, {
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
        }, 'Document downloaded.');
    }

    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">APPLICATION ASSESSMENT</span>
                <h1>{competition.data?.name || 'Review application'}</h1>
                <StatusBadge status={application.status} />
            </div>
            {error && (
                <div className="notice error" role="alert">
                    <p>{error}</p>
                    <button className="secondary" disabled={busy} onClick={reload}>
                        Reload application
                    </button>
                </div>
            )}
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            {!!competition.error && (
                <RequestError error={competition.error} retry={competition.reload} />
            )}
            <section className="panel">
                <h2>Application record</h2>
                <dl className="details-grid">
                    <div>
                        <dt>Application reference</dt>
                        <dd className="record-reference">{application.id}</dd>
                    </div>
                    <div>
                        <dt>Student reference</dt>
                        <dd className="record-reference">{application.studentId}</dd>
                    </div>
                    <div>
                        <dt>Submitted</dt>
                        <dd>{dateTime(application.submittedAtUtc)}</dd>
                    </div>
                </dl>
                <h3>Student note</h3>
                <p className="preserve-lines">{application.note || 'No note provided.'}</p>
                {application.status === 'SUBMITTED' && (
                    <button
                        className="primary"
                        disabled={busy}
                        onClick={() =>
                            action(
                                async () =>
                                    update(
                                        await api<StudentApplication>(base + '/start-review', {
                                            method: 'POST',
                                        }),
                                    ),
                                'Application review started.',
                            )
                        }
                    >
                        Start review
                    </button>
                )}
                {!reviewing && (
                    <p className="muted">
                        Documents and points can be changed only while the application is under
                        review.
                    </p>
                )}
            </section>
            <section className="panel application-section">
                <h2>Supporting documents</h2>
                {documents.loading ? (
                    <p role="status">Loading documents…</p>
                ) : documents.error ? (
                    <RequestError error={documents.error} retry={documents.reload} />
                ) : !documents.data?.length ? (
                    <p>
                        No documents have been submitted. Scoring requires at least one valid
                        document.
                    </p>
                ) : (
                    documents.data.map((document) => (
                        <DocumentReview
                            key={document.id + document.reviewedAtUtc}
                            document={document}
                            editable={reviewing}
                            busy={busy}
                            download={() => download(document)}
                            save={(status, comment) =>
                                action(async () => {
                                    const saved = await api<ApplicationDocument>(
                                        `${base}/documents/${document.id}/review`,
                                        {
                                            method: 'PUT',
                                            body: JSON.stringify({
                                                status,
                                                comment: comment.trim() || null,
                                            }),
                                        },
                                    );
                                    documents.setData(
                                        documents.data!.map((item) =>
                                            item.id === saved.id ? saved : item,
                                        ),
                                    );
                                    score.reload();
                                }, 'Document review saved.')
                            }
                        />
                    ))
                )}
            </section>
            <section className="panel application-section">
                <h2>Assessment points</h2>
                {score.loading ? (
                    <p role="status">Loading assessment…</p>
                ) : score.error ? (
                    <RequestError error={score.error} retry={score.reload} />
                ) : (
                    <StaffScoreForm
                        key={score.data?.calculatedAtUtc || 'new'}
                        score={score.data}
                        editable={reviewing && allValid}
                        busy={busy}
                        save={(values) =>
                            action(async () => {
                                score.setData(
                                    await api<Score>(base + '/score', {
                                        method: 'PUT',
                                        body: JSON.stringify(values),
                                    }),
                                );
                            }, 'Assessment points saved.')
                        }
                    />
                )}
                {reviewing && !allValid && (
                    <p className="muted">
                        Scoring becomes available after all documents have been loaded and marked
                        valid.
                    </p>
                )}
            </section>
        </>
    );
}

function DocumentReview({
    document,
    editable,
    busy,
    download,
    save,
}: {
    document: ApplicationDocument;
    editable: boolean;
    busy: boolean;
    download: () => void;
    save: (status: number, comment: string) => Promise<void>;
}) {
    const [status, setStatus] = useState(document.status === 'INVALID' ? '3' : '2');
    const [comment, setComment] = useState(document.reviewComment || '');
    function submit(event: FormEvent) {
        event.preventDefault();
        if (!editable || busy || (status === '3' && !comment.trim())) return;
        void save(Number(status), comment);
    }
    return (
        <article className="staff-document">
            <div className="heading-row">
                <h3 className="record-reference">{document.fileName}</h3>
                <StatusBadge status={document.status} />
            </div>
            <p className="muted">
                {documentTypes.find(
                    (type) =>
                        type.name.toUpperCase() ===
                        document.documentType.replaceAll('_', '').toUpperCase(),
                )?.label || document.documentType}{' '}
                · {Math.ceil(document.size / 1024)} KB
            </p>
            {document.reviewedAtUtc && (
                <p className="muted">Reviewed: {dateTime(document.reviewedAtUtc)}</p>
            )}
            <button className="secondary" disabled={busy} onClick={download}>
                Download document
            </button>
            {editable ? (
                <form onSubmit={submit} className="application-section">
                    <label>
                        Review outcome
                        <select
                            value={status}
                            disabled={busy}
                            onChange={(event) => setStatus(event.target.value)}
                        >
                            <option value="2">Valid</option>
                            <option value="3">Invalid</option>
                        </select>
                    </label>
                    <label>
                        Review comment
                        <textarea
                            value={comment}
                            rows={3}
                            maxLength={2000}
                            required={status === '3'}
                            disabled={busy}
                            onChange={(event) => setComment(event.target.value)}
                        />
                    </label>
                    <p className="muted">
                        A comment is required for an invalid document. Maximum 2000 characters.
                    </p>
                    <button
                        className="primary"
                        disabled={busy || (status === '3' && !comment.trim())}
                    >
                        Save document review
                    </button>
                </form>
            ) : (
                <p className="preserve-lines">{document.reviewComment || 'No review comment.'}</p>
            )}
        </article>
    );
}
