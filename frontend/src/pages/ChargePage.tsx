import { useState } from 'react';
import { useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Charge } from '../lib/billing';
import { money, chargeTypeLabels } from '../lib/billing';
import { dateTime } from '../lib/applications';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { BillingForm, billingStorageKey } from '../components/BillingForm';
import { BillingPayments } from '../components/BillingPayments';
export function ChargePage({ management = false }: { management?: boolean }) {
    const { id } = useParams();
    const resource = useResource<Charge>(
        '/api/billing/' + (management ? '' : 'me/') + 'charges/' + id,
    );
    if (resource.loading) return <p role="status">Loading charge…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    return (
        <ChargeRecord
            key={id}
            item={resource.data!}
            management={management}
            reload={resource.reload}
        />
    );
}
function ChargeRecord({
    item,
    management,
    reload,
}: {
    item: Charge;
    management: boolean;
    reload: () => void;
}) {
    const [paying, setPaying] = useState(false);
    let pending = false;
    try {
        pending = !!sessionStorage.getItem(billingStorageKey(item.studentId, item.id));
    } catch {
        /* Storage may be unavailable. */
    }
    return (
        <>
            <div className="page-heading heading-row">
                <div>
                    <h1>{chargeTypeLabels[item.type] || 'Charge'}</h1>
                    <StatusBadge status={item.status} />
                </div>
                <button className="secondary" disabled={paying} onClick={reload}>
                    Refresh charge
                </button>
            </div>
            <section className="panel">
                <h2>Charge details</h2>
                <p className="preserve-lines">{item.description}</p>
                <dl className="details-grid">
                    <div>
                        <dt>Amount</dt>
                        <dd>{money(item.amount)}</dd>
                    </div>
                    <div>
                        <dt>Paid</dt>
                        <dd>{money(item.paidAmount)}</dd>
                    </div>
                    <div>
                        <dt>Outstanding</dt>
                        <dd>{money(item.outstandingAmount)}</dd>
                    </div>
                    <div>
                        <dt>Due date</dt>
                        <dd>{item.dueDate}</dd>
                    </div>
                </dl>
                {item.period && <p>Billing month: {item.period}</p>}
                <p className="record-reference">Reference: {item.referenceId}</p>
                <p>Created: {dateTime(item.createdAtUtc)}</p>
                {item.paidAtUtc && <p>Paid in full: {dateTime(item.paidAtUtc)}</p>}
                {item.outstandingAmount === 0 && (
                    <p className="notice success">
                        This charge is fully paid. No further payment is required.
                    </p>
                )}
                {!management && item.outstandingAmount > 0 && (
                    <p className="notice">
                        Contact the billing office for payment instructions. Your balance will
                        update after staff record the received payment.
                    </p>
                )}
                {management && (item.outstandingAmount > 0 || pending) && (
                    <button className="primary" disabled={paying} onClick={() => setPaying(true)}>
                        {pending ? 'Resume pending payment' : 'Record received payment'}
                    </button>
                )}
            </section>
            {paying && (
                <BillingForm
                    studentId={item.studentId}
                    charge={item}
                    cancel={() => setPaying(false)}
                    saved={() => {
                        setPaying(false);
                        reload();
                    }}
                />
            )}
            <BillingPayments
                path={
                    '/api/billing/' +
                    (management ? 'students/' + item.studentId : 'me') +
                    '/payments'
                }
                chargeId={item.id}
            />
        </>
    );
}
