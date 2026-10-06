import { useState } from 'react';
import type { FormEvent } from 'react';
import type { Competition } from '../lib/applications';
import { utcDate } from '../lib/applications';
import { api, errorMessage } from '../lib/api';

function localInput(value: string) {
    const date = utcDate(value);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

export function CompetitionForm({
    competition,
    saved,
}: {
    competition?: Competition;
    saved: (value: Competition) => void;
}) {
    const [year, setYear] = useState(competition?.academicYear || '');
    const [name, setName] = useState(competition?.name || '');
    const [description, setDescription] = useState(competition?.description || '');
    const [start, setStart] = useState(
        competition ? localInput(competition.applicationStartDateUtc) : '',
    );
    const [end, setEnd] = useState(
        competition ? localInput(competition.applicationEndDateUtc) : '',
    );
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (busy) return;
        const startDate = new Date(start);
        const endDate = new Date(end);
        if (
            !year.trim() ||
            !name.trim() ||
            !Number.isFinite(startDate.getTime()) ||
            !Number.isFinite(endDate.getTime()) ||
            startDate >= endDate
        ) {
            setError(
                'Enter the academic year and name, and an end date later than the start date.',
            );
            return;
        }
        setBusy(true);
        setError('');
        try {
            const body = {
                ...(!competition && { academicYear: year.trim() }),
                name: name.trim(),
                description: description.trim() || null,
                applicationStartDateUtc:
                    competition && start === localInput(competition.applicationStartDateUtc)
                        ? utcDate(competition.applicationStartDateUtc).toISOString()
                        : startDate.toISOString(),
                applicationEndDateUtc:
                    competition && end === localInput(competition.applicationEndDateUtc)
                        ? utcDate(competition.applicationEndDateUtc).toISOString()
                        : endDate.toISOString(),
            };
            saved(
                await api<Competition>(
                    '/api/competitions' + (competition ? '/' + competition.id : ''),
                    { method: competition ? 'PUT' : 'POST', body: JSON.stringify(body) },
                ),
            );
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <form onSubmit={submit}>
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            <fieldset disabled={busy} className="competition-fields">
                <div className="form-grid">
                    <label>
                        Academic year
                        <input
                            required
                            maxLength={20}
                            placeholder="2026/2027"
                            value={year}
                            disabled={!!competition}
                            onChange={(event) => setYear(event.target.value)}
                        />
                    </label>
                    <label>
                        Competition name
                        <input
                            required
                            maxLength={200}
                            value={name}
                            onChange={(event) => setName(event.target.value)}
                        />
                    </label>
                </div>
                <label>
                    Description
                    <textarea
                        maxLength={2000}
                        rows={5}
                        value={description}
                        onChange={(event) => setDescription(event.target.value)}
                    />
                </label>
                <div className="form-grid">
                    <label>
                        Applications open
                        <input
                            type="datetime-local"
                            step="1"
                            required
                            value={start}
                            onChange={(event) => setStart(event.target.value)}
                        />
                    </label>
                    <label>
                        Application deadline
                        <input
                            type="datetime-local"
                            step="1"
                            required
                            value={end}
                            onChange={(event) => setEnd(event.target.value)}
                        />
                    </label>
                </div>
                <p className="muted">
                    Enter dates in your device time zone (
                    {Intl.DateTimeFormat().resolvedOptions().timeZone}). The academic year cannot be
                    changed after creation.
                </p>
                <button className="primary" disabled={busy}>
                    {busy ? 'Saving…' : competition ? 'Save draft' : 'Create draft'}
                </button>
            </fieldset>
        </form>
    );
}
