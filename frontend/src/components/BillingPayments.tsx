import { useState } from 'react';
import { useResource } from '../hooks/useResource';
import type { Payment } from '../lib/billing';
import { money, paymentMethodLabels } from '../lib/billing';
import { dateTime } from '../lib/applications';
import { RequestError } from './ApplicationUi';
export function BillingPayments({ path, chargeId }: { path: string; chargeId?: string }) {
    const [page, setPage] = useState(1);
    const resource = useResource<Payment[]>(
        `${path}?page=${page}&pageSize=20${chargeId ? '&chargeId=' + chargeId : ''}`,
    );
    return (
        <section className="panel application-section">
            <h2>Payment history</h2>
            {resource.loading ? (
                <p role="status">Loading payments…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !resource.data?.length ? (
                <p>No payments on this page.</p>
            ) : (
                <div
                    className="ranking-scroll"
                    role="region"
                    aria-label="Payment history"
                    tabIndex={0}
                >
                    <table className="ranking-table">
                        <caption>Recorded payments</caption>
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Amount</th>
                                <th scope="col">Method</th>
                                <th scope="col">Reference</th>
                            </tr>
                        </thead>
                        <tbody>
                            {resource.data.map((item) => (
                                <tr key={item.id}>
                                    <td>{dateTime(item.paymentDateUtc)}</td>
                                    <td>{money(item.amount)}</td>
                                    <td>{paymentMethodLabels[item.method]}</td>
                                    <td className="record-reference">
                                        {item.referenceNumber || '—'}
                                    </td>
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
