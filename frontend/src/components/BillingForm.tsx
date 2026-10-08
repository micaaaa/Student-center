import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, ApiError, errorMessage, getSession } from '../lib/api';
import { chargeTypes, paymentMethods, money, pendingBillingKey } from '../lib/billing';
import type { Charge } from '../lib/billing';
import { ConfirmationDialog } from './ConfirmationDialog';

interface WriteBody {
    requestId: string;
    amount: number;
    studentId?: string;
    type?: number;
    dueDate?: string;
    referenceId?: string;
    description?: string;
    period?: string | null;
    chargeId?: string;
    method?: number;
    referenceNumber?: string | null;
}
export function billingStorageKey(studentId: string, chargeId?: string) {
    return `student-center.billing.${getSession()?.user.id}.${pendingBillingKey(studentId, chargeId)}`;
}
export function BillingForm({
    studentId,
    charge,
    saved,
    cancel,
}: {
    studentId: string;
    charge?: Charge;
    saved: () => void;
    cancel: () => void;
}) {
    const key = billingStorageKey(studentId, charge?.id);
    const [pending, setPending] = useState<WriteBody | null>(() => {
        try {
            return JSON.parse(sessionStorage.getItem(key) || 'null');
        } catch {
            return null;
        }
    });
    const [amount, setAmount] = useState('');
    const [type, setType] = useState(1);
    const [due, setDue] = useState('');
    const [period, setPeriod] = useState('');
    const [reference, setReference] = useState('');
    const [description, setDescription] = useState('');
    const [method, setMethod] = useState(1);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [confirmation, setConfirmation] = useState<WriteBody | null>(null);
    function submit(event: FormEvent) {
        event.preventDefault();
        if (busy) return;
        if (pending) {
            void send(pending);
            return;
        }
        const value = Number(amount);
        if (
            !/^\d+(\.\d{1,2})?$/.test(amount) ||
            value <= 0 ||
            !Number.isSafeInteger(Math.round(value * 100)) ||
            (charge && Math.round(value * 100) > Math.round(charge.outstandingAmount * 100))
        ) {
            setError(
                charge
                    ? `Enter a positive amount with up to two decimal places, no greater than ${money(charge.outstandingAmount)}.`
                    : 'Enter a positive charge amount with up to two decimal places, within the supported limits.',
            );
            return;
        }
        if (
            !charge &&
            (!description.trim() ||
                !due ||
                (type === 1 &&
                    (!/^\d{4}-\d{2}$/.test(period) ||
                        !/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(reference.trim()))))
        ) {
            setError(
                'Complete the charge details. Accommodation requires an accommodation reference and billing month.',
            );
            return;
        }
        if (charge && method !== 1 && !reference.trim()) {
            setError('Enter the transaction reference.');
            return;
        }
        const requestId = crypto.randomUUID();
        setConfirmation(
            charge
                ? {
                      requestId,
                      chargeId: charge.id,
                      amount: value,
                      method,
                      referenceNumber: reference.trim() || null,
                  }
                : {
                      requestId,
                      studentId,
                      type,
                      amount: value,
                      dueDate: due,
                      referenceId: type === 1 ? reference.trim() : requestId,
                      period: type === 1 ? period : null,
                      description: description.trim(),
                  },
        );
    }
    async function send(body: WriteBody) {
        if (busy) return;
        setError('');
        try {
            sessionStorage.setItem(key, JSON.stringify(body));
        } catch {
            setError('Enable browser session storage before recording this transaction.');
            return;
        }
        setPending(body);
        setBusy(true);
        try {
            await api('/api/billing/' + (charge ? 'payments' : 'charges'), {
                method: 'POST',
                body: JSON.stringify(body),
            });
            sessionStorage.removeItem(key);
            setPending(null);
            saved();
        } catch (error) {
            setError(errorMessage(error));
            if (
                error instanceof ApiError &&
                [400, 403, 404, 409].includes(error.status) &&
                !error.message.includes('Retry using the same request ID.')
            ) {
                sessionStorage.removeItem(key);
                setPending(null);
            }
        } finally {
            setBusy(false);
            setConfirmation(null);
        }
    }
    return (
        <section className="panel application-section">
            <h2>{charge ? 'Record received payment' : 'Create charge'}</h2>
            {error && (
                <p className="notice error" role="alert">
                    {error}
                </p>
            )}
            <form onSubmit={submit}>
                <fieldset className="competition-fields" disabled={busy}>
                    {pending ? (
                        <>
                            <p className="notice">
                                A previous request is awaiting confirmation. Retry the same request
                                to prevent duplicate entries.
                            </p>
                            <p>Amount: {money(pending.amount)}</p>
                            <p className="record-reference">
                                Request reference: {pending.requestId}
                            </p>
                        </>
                    ) : (
                        <>
                            <label>
                                Amount (RSD)
                                <input
                                    type="number"
                                    required
                                    min="0.01"
                                    max={charge?.outstandingAmount}
                                    step="0.01"
                                    value={amount}
                                    onChange={(e) => setAmount(e.target.value)}
                                />
                            </label>
                            {charge ? (
                                <>
                                    <p>Outstanding: {money(charge.outstandingAmount)}</p>
                                    <button
                                        type="button"
                                        className="secondary"
                                        onClick={() =>
                                            setAmount(charge.outstandingAmount.toFixed(2))
                                        }
                                    >
                                        Use full outstanding amount
                                    </button>
                                    {Number(amount) > 0 &&
                                        Number(amount) <= charge.outstandingAmount && (
                                            <p>
                                                Remaining after this payment:{' '}
                                                {money(
                                                    (Math.round(charge.outstandingAmount * 100) -
                                                        Math.round(Number(amount) * 100)) /
                                                        100,
                                                )}
                                            </p>
                                        )}
                                    <label>
                                        Payment method
                                        <select
                                            value={method}
                                            onChange={(e) => setMethod(Number(e.target.value))}
                                        >
                                            {paymentMethods.map((label, i) => (
                                                <option key={label} value={i + 1}>
                                                    {label}
                                                </option>
                                            ))}
                                        </select>
                                    </label>
                                    <label>
                                        Transaction reference{method === 1 ? ' (optional)' : ''}
                                        <input
                                            required={method !== 1}
                                            maxLength={100}
                                            value={reference}
                                            onChange={(e) => setReference(e.target.value)}
                                        />
                                    </label>
                                    <p className="muted">
                                        Record payment already received. This form does not process
                                        card or bank payments.
                                    </p>
                                </>
                            ) : (
                                <>
                                    <label>
                                        Charge type
                                        <select
                                            value={type}
                                            onChange={(e) => {
                                                setType(Number(e.target.value));
                                                setReference('');
                                            }}
                                        >
                                            {Object.entries(chargeTypes).map(([value, label]) => (
                                                <option key={value} value={value}>
                                                    {label}
                                                </option>
                                            ))}
                                        </select>
                                    </label>
                                    <label>
                                        Due date
                                        <input
                                            type="date"
                                            required
                                            value={due}
                                            onChange={(e) => setDue(e.target.value)}
                                        />
                                    </label>
                                    {type === 1 && (
                                        <>
                                            <label>
                                                Accommodation reference
                                                <input
                                                    required
                                                    value={reference}
                                                    onChange={(e) => setReference(e.target.value)}
                                                />
                                            </label>
                                            <label>
                                                Billing month
                                                <input
                                                    type="month"
                                                    required
                                                    value={period}
                                                    onChange={(e) => setPeriod(e.target.value)}
                                                />
                                            </label>
                                        </>
                                    )}
                                    <label>
                                        Description
                                        <textarea
                                            required
                                            rows={3}
                                            maxLength={1000}
                                            value={description}
                                            onChange={(e) => setDescription(e.target.value)}
                                        />
                                    </label>
                                    <p className="muted">
                                        Only one accommodation charge is allowed per accommodation
                                        and billing month. Meal purchases are already paid and do
                                        not create charges.
                                    </p>
                                </>
                            )}
                        </>
                    )}
                    <div className="button-row application-section">
                        <button className="primary">
                            {pending
                                ? 'Retry pending request'
                                : charge
                                  ? 'Review payment'
                                  : 'Review charge'}
                        </button>
                        <button className="secondary" type="button" onClick={cancel}>
                            {pending ? 'Close' : 'Cancel'}
                        </button>
                    </div>
                </fieldset>
            </form>
            {confirmation && (
                <ConfirmationDialog
                    title={charge ? 'Confirm received payment' : 'Confirm charge'}
                    busy={busy}
                    onClose={() => setConfirmation(null)}
                    onConfirm={() => send(confirmation)}
                >
                    <p>{money(confirmation.amount)}</p>
                    {charge ? (
                        <p>
                            {paymentMethods[confirmation.method! - 1]} ·{' '}
                            {confirmation.referenceNumber || 'No reference'}
                            <br />
                            Confirm that this payment has been received.
                            <br />
                            Remaining after payment:{' '}
                            {money(
                                (Math.round(charge.outstandingAmount * 100) -
                                    Math.round(confirmation.amount * 100)) /
                                    100,
                            )}
                        </p>
                    ) : (
                        <>
                            <p>{confirmation.description}</p>
                            <p>
                                {chargeTypes[confirmation.type!]} · Due {confirmation.dueDate}
                            </p>
                            {confirmation.period && <p>Billing month: {confirmation.period}</p>}
                        </>
                    )}
                </ConfirmationDialog>
            )}
        </section>
    );
}
