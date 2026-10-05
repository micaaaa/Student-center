import { useState } from 'react';
import type { FormEvent } from 'react';
import type { ConclusionSettings } from '../lib/results';
import { dateTime, utcDate } from '../lib/applications';

export function ConclusionSettingsForm({
    settings,
    disabled,
    save,
}: {
    settings: ConclusionSettings;
    disabled: boolean;
    save: (body: object) => Promise<void>;
}) {
    const [places, setPlaces] = useState(
        settings.availablePlaces === null ? '' : String(settings.availablePlaces),
    );
    const [deadline, setDeadline] = useState('');
    const [error, setError] = useState('');
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (disabled) return;
        setError('');
        const capacity = Number(places);
        const date = deadline
            ? new Date(deadline)
            : settings.appealDeadlineUtc
              ? utcDate(settings.appealDeadlineUtc)
              : null;
        if (
            !places.trim() ||
            !Number.isInteger(capacity) ||
            capacity < 0 ||
            capacity > 2147483647 ||
            !date ||
            !Number.isFinite(date.getTime())
        ) {
            setError('Enter a valid number of places and an appeal deadline.');
            return;
        }
        if (
            (!settings.appealDeadlineUtc && date.getTime() <= Date.now()) ||
            (settings.appealDeadlineUtc && date < utcDate(settings.appealDeadlineUtc))
        ) {
            setError(
                'The initial deadline must be in the future. An announced deadline cannot be shortened.',
            );
            return;
        }
        await save({ availablePlaces: capacity, appealDeadlineUtc: date.toISOString() });
    }
    return (
        <form onSubmit={submit}>
            <p>
                Announced deadline:{' '}
                {settings.appealDeadlineUtc
                    ? dateTime(settings.appealDeadlineUtc)
                    : 'Not configured'}
            </p>
            <div className="form-grid">
                <label>
                    Available places
                    <input
                        type="number"
                        min="0"
                        max="2147483647"
                        step="1"
                        required
                        value={places}
                        disabled={disabled}
                        onChange={(event) => setPlaces(event.target.value)}
                    />
                </label>
                <label>
                    {settings.appealDeadlineUtc
                        ? 'Extend appeal deadline (optional)'
                        : 'Appeal deadline'}
                    <input
                        type="datetime-local"
                        required={!settings.appealDeadlineUtc}
                        value={deadline}
                        disabled={disabled}
                        onChange={(event) => setDeadline(event.target.value)}
                    />
                </label>
            </div>
            <p className="muted">
                Enter the deadline in your device time zone (
                {Intl.DateTimeFormat().resolvedOptions().timeZone}). Leave an existing deadline
                unchanged when adjusting capacity.
            </p>
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            <button className="primary" disabled={disabled}>
                Save conclusion settings
            </button>
        </form>
    );
}
