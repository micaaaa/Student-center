import { useState } from 'react';
import { useResource } from '../hooks/useResource';
import { dateTime } from '../lib/applications';
import { mealTypes } from '../lib/food';
import type { Purchase, Consumption } from '../lib/food';
import { RequestError } from './ApplicationUi';

export function MealHistory({ path, purchases }: { path: string; purchases: boolean }) {
    const [page, setPage] = useState(1);
    const resource = useResource<(Purchase & Consumption)[]>(path + `&page=${page}&pageSize=20`);
    return (
        <section className="panel application-section">
            <h2>{purchases ? 'Paid meal purchases' : 'Meal consumption history'}</h2>
            {resource.loading ? (
                <p role="status">Loading history…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !resource.data?.length ? (
                <p>No records on this page.</p>
            ) : (
                <div
                    className="ranking-scroll"
                    role="region"
                    aria-label={purchases ? 'Purchase history' : 'Consumption history'}
                    tabIndex={0}
                >
                    <table className="ranking-table">
                        <caption>
                            {purchases ? 'Recorded purchases' : 'Recorded consumptions'}
                        </caption>
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Meal type</th>
                                {purchases ? (
                                    <>
                                        <th scope="col">Quantity</th>
                                        <th scope="col">Unit price</th>
                                        <th scope="col">Amount</th>
                                    </>
                                ) : (
                                    <th scope="col">Restaurant reference</th>
                                )}
                            </tr>
                        </thead>
                        <tbody>
                            {resource.data.map((item) => (
                                <tr key={item.id}>
                                    <td>
                                        {dateTime(
                                            purchases ? item.purchasedAtUtc : item.consumedAtUtc,
                                        )}
                                    </td>
                                    <td>{mealTypes[item.mealType - 1]}</td>
                                    {purchases ? (
                                        <>
                                            <td>{item.quantity}</td>
                                            <td>{item.unitPrice.toFixed(2)}</td>
                                            <td>{item.amount.toFixed(2)}</td>
                                        </>
                                    ) : (
                                        <td className="record-reference">{item.restaurantId}</td>
                                    )}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
            <div className="button-row application-section">
                <button
                    className="secondary"
                    disabled={page === 1 || resource.loading}
                    onClick={() => setPage(page - 1)}
                >
                    Previous
                </button>
                <span>Page {page}</span>
                <button
                    className="secondary"
                    disabled={
                        resource.loading ||
                        !!resource.error ||
                        resource.data?.length !== 20 ||
                        page >= 107374182
                    }
                    onClick={() => setPage(page + 1)}
                >
                    Next
                </button>
            </div>
        </section>
    );
}
