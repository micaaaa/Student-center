import { useState } from 'react';
import { StudentSearch } from '../components/StudentSearch';
import { Link, useSearchParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Student } from '../lib/types';
import type { Charge, Balance } from '../lib/billing';
import { money, chargeTypeLabels } from '../lib/billing';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { BillingForm } from '../components/BillingForm';
import { BillingPayments } from '../components/BillingPayments';

export function BillingPage({ management = false }: { management?: boolean }) {
    const [search, setSearch] = useSearchParams();
    const studentId = search.get('studentId') || '';
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">FINANCIAL SERVICES</span>
                <h1>{management ? 'Billing administration' : 'My billing'}</h1>
            </div>
            {management ? (
                <>
                    {studentId ? (
                        <button
                            className="secondary"
                            onClick={() =>
                                setSearch((current) => {
                                    const next = new URLSearchParams(current);
                                    next.delete('studentId');
                                    return next;
                                })
                            }
                        >
                            Choose another student
                        </button>
                    ) : (
                        <StudentSearch
                            onSelect={(id) =>
                                setSearch((current) => {
                                    const next = new URLSearchParams(current);
                                    next.set('studentId', id);
                                    return next;
                                })
                            }
                        />
                    )}
                    {studentId && <StaffBilling key={studentId} studentId={studentId} />}
                </>
            ) : (
                <BillingRecords />
            )}
        </>
    );
}
function StaffBilling({ studentId }: { studentId: string }) {
    const student = useResource<Student>('/api/students/' + studentId);
    if (student.loading) return <p role="status">Loading student…</p>;
    if (student.error) return <RequestError error={student.error} retry={student.reload} />;
    return (
        <>
            <h2 className="application-section">
                {student.data!.firstName} {student.data!.lastName} · {student.data!.studentNumber}
            </h2>
            <BillingRecords studentId={studentId} />
        </>
    );
}
function BillingRecords({ studentId }: { studentId?: string }) {
    const base = '/api/billing/' + (studentId ? 'students/' + studentId : 'me');
    const [page, setPage] = useState(1);
    const [overdue, setOverdue] = useState(false);
    const [creating, setCreating] = useState(false);
    const [version, setVersion] = useState(0);
    const balance = useResource<Balance>(base + '/balance');
    const charges = useResource<Charge[]>(
        `${base}/charges?page=${page}&pageSize=20&overdueOnly=${overdue}`,
    );
    function refresh() {
        balance.reload();
        charges.reload();
        setVersion((v) => v + 1);
    }
    return (
        <>
            <section className="panel application-section">
                <div className="heading-row">
                    <h2>Account balance</h2>
                    <button
                        className="secondary"
                        disabled={creating || balance.loading || charges.loading}
                        onClick={refresh}
                    >
                        Refresh billing
                    </button>
                </div>
                {balance.loading ? (
                    <p role="status">Loading balance…</p>
                ) : balance.error ? (
                    <RequestError error={balance.error} retry={balance.reload} />
                ) : (
                    <dl className="details-grid">
                        <div>
                            <dt>Total charged</dt>
                            <dd>{money(balance.data!.totalChargedAmount)}</dd>
                        </div>
                        <div>
                            <dt>Total paid</dt>
                            <dd>{money(balance.data!.totalPaidAmount)}</dd>
                        </div>
                        <div>
                            <dt>Outstanding</dt>
                            <dd>{money(balance.data!.outstandingAmount)}</dd>
                        </div>
                        <div>
                            <dt>Overdue</dt>
                            <dd>{money(balance.data!.overdueAmount)}</dd>
                        </div>
                    </dl>
                )}
                {studentId && (
                    <button
                        className="primary"
                        disabled={creating}
                        onClick={() => setCreating(true)}
                    >
                        Create charge
                    </button>
                )}
            </section>
            {creating && studentId && (
                <BillingForm
                    studentId={studentId}
                    cancel={() => setCreating(false)}
                    saved={() => {
                        setCreating(false);
                        refresh();
                    }}
                />
            )}
            <div className="list-toolbar application-section">
                <label>
                    Charge filter
                    <select
                        value={String(overdue)}
                        onChange={(e) => {
                            setOverdue(e.target.value === 'true');
                            setPage(1);
                        }}
                    >
                        <option value="false">All charges</option>
                        <option value="true">Overdue charges</option>
                    </select>
                </label>
            </div>
            {charges.loading ? (
                <p role="status">Loading charges…</p>
            ) : charges.error ? (
                <RequestError error={charges.error} retry={charges.reload} />
            ) : !charges.data?.length ? (
                <p className="panel">No charges on this page.</p>
            ) : (
                <div className="competition-list">
                    {charges.data.map((item) => (
                        <article className="panel" key={item.id}>
                            <StatusBadge status={item.status} />
                            <h2>{chargeTypeLabels[item.type] || 'Charge'}</h2>
                            <p className="preserve-lines">{item.description}</p>
                            <p>Due: {item.dueDate}</p>
                            <p>
                                Amount: {money(item.amount)} · Outstanding:{' '}
                                {money(item.outstandingAmount)}
                            </p>
                            <Link
                                className="secondary"
                                to={
                                    (studentId
                                        ? '/staff/billing/charges/'
                                        : '/my-billing/charges/') + item.id
                                }
                            >
                                View charge and payments
                            </Link>
                        </article>
                    ))}
                </div>
            )}
            <div className="button-row application-section">
                <button
                    className="secondary"
                    disabled={charges.loading || page === 1}
                    onClick={() => setPage(page - 1)}
                >
                    Previous
                </button>
                <span>Page {page}</span>
                <button
                    className="secondary"
                    disabled={
                        charges.loading ||
                        !!charges.error ||
                        charges.data?.length !== 20 ||
                        page >= 107374182
                    }
                    onClick={() => setPage(page + 1)}
                >
                    Next
                </button>
            </div>
            <BillingPayments key={version} path={base + '/payments'} />
        </>
    );
}
