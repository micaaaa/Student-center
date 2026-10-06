import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, errorMessage } from '../lib/api';
import { mealTypes } from '../lib/food';
import type { Entitlement } from '../lib/food';

export function EntitlementForm({
    studentId,
    year,
    month,
    item,
    saved,
    cancel,
}: {
    studentId: string;
    year: number;
    month: number;
    item?: Entitlement;
    saved: () => void;
    cancel: () => void;
}) {
    const [academicYear, setAcademicYear] = useState(
        item?.academicYear || `${month >= 10 ? year : year - 1}/${month >= 10 ? year + 1 : year}`,
    );
    const [type, setType] = useState(item?.mealType || 1);
    const [quantity, setQuantity] = useState(String(item?.allowedQuantity || ''));
    const [status, setStatus] = useState(item?.status || 1);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (busy) return;
        const years = academicYear.split('/').map(Number);
        if (
            (!item &&
                (!/^\d{4}\/\d{4}$/.test(academicYear) ||
                    years[0] < 1 ||
                    years[1] !== years[0] + 1 ||
                    !years.includes(year))) ||
            !Number.isInteger(Number(quantity)) ||
            Number(quantity) < Math.max(1, item?.consumedQuantity || 0) ||
            Number(quantity) > 2147483647
        ) {
            setError(
                'Check the academic year and allowed quantity. The academic year must contain consecutive years and include the selected calendar year.',
            );
            return;
        }
        setBusy(true);
        setError('');
        try {
            await api('/api/meal-entitlements' + (item ? '/' + item.id : ''), {
                method: item ? 'PUT' : 'POST',
                body: JSON.stringify(
                    item
                        ? { allowedQuantity: Number(quantity), status }
                        : {
                              studentId,
                              academicYear,
                              year,
                              month,
                              mealType: type,
                              allowedQuantity: Number(quantity),
                          },
                ),
            });
            saved();
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <section className="panel application-section">
            <h2>{item ? 'Edit meal entitlement' : 'Create meal entitlement'}</h2>
            <form onSubmit={submit}>
                {error && (
                    <p className="notice error" role="alert">
                        {error}
                    </p>
                )}
                <fieldset className="competition-fields" disabled={busy}>
                    <div className="form-grid">
                        <label>
                            Academic year
                            <input
                                required
                                maxLength={9}
                                disabled={!!item}
                                value={academicYear}
                                onChange={(e) => setAcademicYear(e.target.value)}
                            />
                        </label>
                        <label>
                            Meal type
                            <select
                                value={type}
                                disabled={!!item}
                                onChange={(e) => setType(Number(e.target.value))}
                            >
                                {mealTypes.map((name, i) => (
                                    <option value={i + 1} key={name}>
                                        {name}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label>
                            Allowed quantity
                            <input
                                required
                                type="number"
                                min={Math.max(1, item?.consumedQuantity || 0)}
                                max="2147483647"
                                step="1"
                                value={quantity}
                                onChange={(e) => setQuantity(e.target.value)}
                            />
                        </label>
                        {item && (
                            <label>
                                Status
                                <select
                                    value={status}
                                    onChange={(e) => setStatus(Number(e.target.value))}
                                >
                                    <option value={1}>Active</option>
                                    <option value={2}>Suspended</option>
                                </select>
                            </label>
                        )}
                    </div>
                    <p className="muted">
                        This sets the total entitlement quantity. Use Record paid purchase to add
                        purchased meals with a payment record.
                    </p>
                    <div className="button-row">
                        <button className="primary">Save entitlement</button>
                        <button className="secondary" type="button" onClick={cancel}>
                            Cancel
                        </button>
                    </div>
                </fieldset>
            </form>
        </section>
    );
}
