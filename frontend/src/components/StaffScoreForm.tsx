import { useState } from 'react';
import type { FormEvent } from 'react';
import type { Score } from '../lib/results';
import { dateTime } from '../lib/applications';

const categories = [
    ['academicPoints', 'Academic achievement'],
    ['incomePoints', 'Household income'],
    ['ectsPoints', 'ECTS credits'],
    ['studyYearPoints', 'Study year'],
    ['additionalPoints', 'Additional criteria'],
] as const;
type Category = (typeof categories)[number][0];

export function StaffScoreForm({
    score,
    editable,
    busy,
    save,
}: {
    score: Score | null;
    editable: boolean;
    busy: boolean;
    save: (values: Record<Category, number>) => Promise<void>;
}) {
    const [values, setValues] = useState(
        () =>
            Object.fromEntries(
                categories.map(([key]) => [key, score ? String(score[key]) : '']),
            ) as Record<Category, string>,
    );
    const valid = categories.every(
        ([key]) =>
            /^\d+(\.\d{1,2})?$/.test(values[key]) &&
            Number.isSafeInteger(Math.round(Number(values[key]) * 100)),
    );
    const totalCents = categories.reduce(
        (sum, [key]) => sum + Math.round(Number(values[key]) * 100),
        0,
    );
    const safeTotal = valid && Number.isSafeInteger(totalCents);
    function submit(event: FormEvent) {
        event.preventDefault();
        if (!editable || busy || !safeTotal) return;
        void save(
            Object.fromEntries(categories.map(([key]) => [key, Number(values[key])])) as Record<
                Category,
                number
            >,
        );
    }
    return (
        <>
            {score ? (
                <>
                    <p>
                        Saved total: <strong>{score.totalPoints}</strong> · Calculated:{' '}
                        {dateTime(score.calculatedAtUtc)}
                    </p>
                    {!score.isCurrent && (
                        <p className="notice" role="status">
                            The saved score is no longer current. Reassess the points after document
                            review.
                        </p>
                    )}
                </>
            ) : (
                <p className="muted">This application has not been scored yet.</p>
            )}
            <form onSubmit={submit}>
                <div className="form-grid">
                    {categories.map(([key, label]) => (
                        <label key={key}>
                            {label}
                            <input
                                type="number"
                                min="0"
                                step="0.01"
                                required
                                value={values[key]}
                                disabled={!editable || busy}
                                onChange={(event) =>
                                    setValues({ ...values, [key]: event.target.value })
                                }
                            />
                        </label>
                    ))}
                </div>
                <p className="muted">
                    Enter all five categories using non-negative values with up to two decimal
                    places.
                </p>
                <p>
                    Assessment total:{' '}
                    <strong>{safeTotal ? (totalCents / 100).toFixed(2) : '—'}</strong>
                </p>
                {editable && (
                    <button className="primary" disabled={busy || !safeTotal}>
                        Save assessment
                    </button>
                )}
            </form>
        </>
    );
}
