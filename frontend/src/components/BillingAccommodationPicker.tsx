import { useEffect, useState } from 'react';
import { api, errorMessage } from '../lib/api';

interface AccommodationOption {
    id: string;
    academicYear: string;
    status: string;
    isActive: boolean;
    roomNumber: string;
    dormName: string;
}

export function BillingAccommodationPicker({
    studentId,
    value,
    onChange,
}: {
    studentId: string;
    value: string;
    onChange: (value: string) => void;
}) {
    const [options, setOptions] = useState<AccommodationOption[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [attempt, setAttempt] = useState(0);

    useEffect(() => {
        let cancelled = false;
        setLoading(true);
        setError('');
        setOptions([]);
        onChange('');
        api<AccommodationOption[]>(
            `/api/accommodations/billing-options?studentId=${encodeURIComponent(studentId)}`,
        )
            .then((items) => {
                if (cancelled) return;
                setOptions(items);
                const current = items.find((item) => item.isActive);
                onChange(current?.id ?? (items.length === 1 ? items[0].id : ''));
            })
            .catch((error) => {
                if (!cancelled) setError(errorMessage(error));
            })
            .finally(() => {
                if (!cancelled) setLoading(false);
            });
        return () => {
            cancelled = true;
        };
    }, [studentId, attempt, onChange]);

    return (
        <>
            <label>
                Accommodation
                <select
                    required
                    value={value}
                    disabled={loading || !!error || options.length === 0}
                    onChange={(event) => onChange(event.target.value)}
                >
                    <option value="">
                        {loading ? 'Loading accommodation…' : 'Select accommodation'}
                    </option>
                    {options.map((item) => (
                        <option key={item.id} value={item.id}>
                            {item.dormName} · Room {item.roomNumber} · {item.academicYear} ·{' '}
                            {item.status === 'ACTIVE'
                                ? 'Moved in'
                                : item.status === 'ASSIGNED'
                                  ? 'Assigned'
                                  : 'Completed'}
                        </option>
                    ))}
                </select>
            </label>
            {error && (
                <div className="notice error" role="alert">
                    {error}{' '}
                    <button
                        type="button"
                        className="secondary"
                        onClick={() => setAttempt((value) => value + 1)}
                    >
                        Retry
                    </button>
                </div>
            )}
            {!loading && !error && options.length === 0 && (
                <p className="notice">
                    This student has no accommodation available for billing. Assign a room before
                    creating an accommodation charge.
                </p>
            )}
        </>
    );
}
