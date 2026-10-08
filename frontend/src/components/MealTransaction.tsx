import { useState } from 'react';
import type { FormEvent } from 'react';
import { useResource } from '../hooks/useResource';
import { api, ApiError, errorMessage, getSession } from '../lib/api';
import type { Entitlement, Restaurant } from '../lib/food';
import { ConfirmationDialog } from './ConfirmationDialog';
import { RequestError } from './ApplicationUi';
import { money } from '../lib/billing';

type Pending = {
    requestId: string;
    entitlementId: string;
    quantity?: number;
    unitPrice?: number;
    restaurantId?: string;
    cardReference?: string | null;
};

export function MealTransaction({
    item,
    purchase,
    saved,
    cancel,
}: {
    item: Entitlement;
    purchase: boolean;
    saved: () => void;
    cancel: () => void;
}) {
    const key = `student-center.meal-request.${getSession()?.user.id}.${item.id}.${purchase ? 'purchase' : 'consume'}`;
    const [pending, setPending] = useState<Pending | null>(() => {
        try {
            return JSON.parse(sessionStorage.getItem(key) || 'null');
        } catch {
            return null;
        }
    });
    const [quantity, setQuantity] = useState('1');
    const [price, setPrice] = useState('');
    const [restaurantId, setRestaurantId] = useState('');
    const [card, setCard] = useState('');
    const [busy, setBusy] = useState(false);
    const [confirmation, setConfirmation] = useState<Pending | null>(null);
    const [error, setError] = useState('');
    const restaurants = useResource<Restaurant[]>('/api/restaurants');
    function submit(event: FormEvent) {
        event.preventDefault();
        if (busy) return;
        if (pending) {
            void send(pending);
            return;
        }
        if (
            purchase &&
            (!Number.isInteger(Number(quantity)) ||
                Number(quantity) < 1 ||
                Number(quantity) > 2147483647 - item.allowedQuantity ||
                !/^\d+(\.\d{1,2})?$/.test(price) ||
                Number(price) <= 0 ||
                Number(price) > 99999999.99 ||
                !Number.isSafeInteger(Math.round(Number(price) * 100) * Number(quantity)))
        ) {
            setError(
                'Enter a positive quantity and unit price with up to two decimal places, within the supported limits.',
            );
            return;
        }
        if (
            !purchase &&
            (restaurants.loading ||
                restaurants.error ||
                !restaurants.data?.some((r) => r.id === restaurantId && r.status === 1))
        )
            return;
        setConfirmation({
            requestId: crypto.randomUUID(),
            entitlementId: item.id,
            ...(purchase
                ? { quantity: Number(quantity), unitPrice: Number(price) }
                : { restaurantId, cardReference: card.trim() || null }),
        });
    }
    async function send(body: Pending) {
        if (busy) return;
        setError('');
        try {
            sessionStorage.setItem(key, JSON.stringify(body));
        } catch {
            setError(
                'Browser storage is unavailable. Enable session storage before recording this transaction.',
            );
            return;
        }
        setPending(body);
        setBusy(true);
        try {
            await api(purchase ? '/api/meal-purchases' : '/api/meal-consumptions', {
                method: 'POST',
                body: JSON.stringify(body),
            });
            sessionStorage.removeItem(key);
            setPending(null);
            saved();
        } catch (error) {
            setError(errorMessage(error));
            if (error instanceof ApiError && [400, 403, 404, 409].includes(error.status)) {
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
            <h2>{purchase ? 'Record paid purchase' : 'Record meal consumption'}</h2>
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
                                The previous request has not been confirmed. Retry the same request
                                to avoid a duplicate transaction.
                            </p>
                            <p className="record-reference">
                                Request reference: {pending.requestId}
                            </p>
                            {purchase ? (
                                <p>
                                    Quantity: {pending.quantity} · Unit price:{' '}
                                    {pending.unitPrice?.toFixed(2)}
                                </p>
                            ) : (
                                <p className="record-reference">
                                    Restaurant reference: {pending.restaurantId}
                                </p>
                            )}
                        </>
                    ) : purchase ? (
                        <>
                            <p>
                                This records meals that have already been paid for. It does not
                                collect payment or create an unpaid charge.
                            </p>
                            <div className="form-grid">
                                <label>
                                    Purchase quantity
                                    <input
                                        type="number"
                                        required
                                        min="1"
                                        max={2147483647 - item.allowedQuantity}
                                        step="1"
                                        value={quantity}
                                        onChange={(e) => setQuantity(e.target.value)}
                                    />
                                </label>
                                <label>
                                    Unit price (RSD)
                                    <input
                                        type="number"
                                        required
                                        min="0.01"
                                        max="99999999.99"
                                        step="0.01"
                                        value={price}
                                        onChange={(e) => setPrice(e.target.value)}
                                    />
                                </label>
                            </div>
                            {Number(quantity) > 0 &&
                                Number(price) > 0 &&
                                Number.isFinite(Number(quantity) * Number(price)) && (
                                    <p>
                                        Total already paid:{' '}
                                        <strong>{money(Number(quantity) * Number(price))}</strong>
                                    </p>
                                )}
                        </>
                    ) : (
                        <>
                            {restaurants.loading ? (
                                <p role="status">Loading restaurants…</p>
                            ) : restaurants.error ? (
                                <RequestError
                                    error={restaurants.error}
                                    retry={restaurants.reload}
                                />
                            ) : !restaurants.data?.some((restaurant) => restaurant.status === 1) ? (
                                <p className="notice">
                                    No active restaurants are available. Activate a restaurant in
                                    restaurant administration before recording consumption.
                                </p>
                            ) : (
                                <label>
                                    Restaurant
                                    <select
                                        required
                                        value={restaurantId}
                                        onChange={(e) => setRestaurantId(e.target.value)}
                                    >
                                        <option value="">Select a restaurant</option>
                                        {restaurants.data
                                            ?.filter((r) => r.status === 1)
                                            .map((r) => (
                                                <option key={r.id} value={r.id}>
                                                    {r.name}
                                                </option>
                                            ))}
                                    </select>
                                </label>
                            )}
                            <label>
                                Card reference (optional)
                                <input
                                    maxLength={100}
                                    value={card}
                                    onChange={(e) => setCard(e.target.value)}
                                />
                            </label>
                            <p>One meal will be deducted from the remaining quantity.</p>
                        </>
                    )}
                    <div className="button-row">
                        <button
                            className="primary"
                            disabled={
                                busy ||
                                (!purchase &&
                                    !pending &&
                                    (restaurants.loading || !!restaurants.error || !restaurantId))
                            }
                        >
                            {pending
                                ? 'Retry pending request'
                                : purchase
                                  ? 'Confirm paid purchase'
                                  : 'Confirm consumption'}
                        </button>
                        <button type="button" className="secondary" onClick={cancel}>
                            {pending ? 'Close' : 'Cancel'}
                        </button>
                    </div>
                </fieldset>
            </form>
            {confirmation && (
                <ConfirmationDialog
                    title={purchase ? 'Confirm paid purchase' : 'Confirm meal consumption'}
                    busy={busy}
                    onClose={() => setConfirmation(null)}
                    onConfirm={() => send(confirmation)}
                >
                    <p>
                        {purchase
                            ? `${confirmation.quantity} meals × ${money(confirmation.unitPrice!)} = ${money(confirmation.quantity! * confirmation.unitPrice!)}. Confirm that payment has already been received.`
                            : 'Record one consumed meal at the selected restaurant?'}
                    </p>
                </ConfirmationDialog>
            )}
        </section>
    );
}
