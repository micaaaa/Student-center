import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router';
import { StatusBadge } from './ApplicationUi';
import { dateTime } from '../lib/applications';
import type { StaffAppeal } from '../lib/staffRankings';
import { ConfirmationDialog } from './ConfirmationDialog';

export function StaffAppealCard({
    appeal,
    disabled,
    start,
    resolve,
}: {
    appeal: StaffAppeal;
    disabled: boolean;
    start: () => Promise<void>;
    resolve: (accepted: boolean, response: string) => Promise<void>;
}) {
    const [decision, setDecision] = useState('');
    const [response, setResponse] = useState('');
    const [confirm, setConfirm] = useState(false);
    function submit(event: FormEvent) {
        event.preventDefault();
        if (!disabled && decision && response.trim()) setConfirm(true);
    }
    return (
        <article className="staff-document">
            <StatusBadge status={appeal.status} />
            <p className="record-reference">
                Application:{' '}
                <Link to={'/staff/applications/' + appeal.applicationId}>
                    {appeal.applicationId}
                </Link>
            </p>
            <p className="muted">Submitted: {dateTime(appeal.submittedAtUtc)}</p>
            <h3>Reason for appeal</h3>
            <p className="preserve-lines">{appeal.reason}</p>
            {appeal.response && (
                <>
                    <h3>Official response</h3>
                    <p className="preserve-lines">{appeal.response}</p>
                    <p className="muted">
                        Resolved:{' '}
                        {appeal.resolvedAtUtc ? dateTime(appeal.resolvedAtUtc) : 'Pending'}
                    </p>
                </>
            )}
            {appeal.status === 'SUBMITTED' && (
                <button className="primary" disabled={disabled} onClick={start}>
                    Start appeal review
                </button>
            )}
            {appeal.status === 'UNDER_REVIEW' && (
                <form onSubmit={submit}>
                    <p className="muted">
                        Before accepting an appeal, review the application documents and recalculate
                        its points after the appeal submission.
                    </p>
                    <label>
                        Appeal decision
                        <select
                            required
                            value={decision}
                            disabled={disabled}
                            onChange={(event) => setDecision(event.target.value)}
                        >
                            <option value="">Select a decision</option>
                            <option value="accept">Accept</option>
                            <option value="reject">Reject</option>
                        </select>
                    </label>
                    <label>
                        Official response
                        <textarea
                            rows={4}
                            required
                            maxLength={4000}
                            value={response}
                            disabled={disabled}
                            onChange={(event) => setResponse(event.target.value)}
                        />
                    </label>
                    <button
                        className="primary"
                        disabled={disabled || !decision || !response.trim()}
                    >
                        Save appeal decision
                    </button>
                </form>
            )}
            {confirm && (
                <ConfirmationDialog
                    title="Confirm appeal decision"
                    busy={disabled}
                    onClose={() => setConfirm(false)}
                    onConfirm={async () => {
                        if (disabled) return;
                        await resolve(decision === 'accept', response.trim());
                        setConfirm(false);
                    }}
                >
                    <p>
                        {decision === 'accept' ? 'Accept' : 'Reject'} this appeal with the following
                        official response?
                    </p>
                    <p className="preserve-lines">{response}</p>
                    <p>The saved decision cannot be edited.</p>
                </ConfirmationDialog>
            )}
        </article>
    );
}
