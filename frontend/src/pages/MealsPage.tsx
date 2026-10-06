import { useState } from 'react';
import { useSearchParams } from 'react-router';
import { StudentSearch } from '../components/StudentSearch';
import { useResource } from '../hooks/useResource';
import { getSession } from '../lib/api';
import { mealTypes } from '../lib/food';
import type { Entitlement } from '../lib/food';
import type { Student } from '../lib/types';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { EntitlementForm } from '../components/EntitlementForm';
import { MealTransaction } from '../components/MealTransaction';
import { MealHistory } from '../components/MealHistory';

export function MealsPage({ management = false }: { management?: boolean }) {
    const [period, setPeriod] = useState(() => new Date().toISOString().slice(0, 7));
    const [search, setSearch] = useSearchParams();
    const studentId = search.get('studentId') || '';
    const [year, month] = period.split('-').map(Number);
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">DINING SERVICES</span>
                <h1>{management ? 'Meal administration' : 'My meals'}</h1>
                <p className="muted">
                    Monthly entitlements, paid purchases and consumption records.
                </p>
            </div>
            <div className="list-toolbar">
                <label>
                    Meal period (UTC)
                    <input
                        type="month"
                        min="0001-01"
                        max="9998-12"
                        required
                        value={period}
                        onChange={(e) => setPeriod(e.target.value)}
                    />
                </label>
            </div>
            {management &&
                (studentId ? (
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
                ))}
            {year >= 1 &&
                year <= 9998 &&
                month >= 1 &&
                month <= 12 &&
                (!management || studentId) &&
                (management ? (
                    <StaffMeals
                        key={studentId + period}
                        studentId={studentId}
                        year={year}
                        month={month}
                    />
                ) : (
                    <MealRecords key={period} year={year} month={month} />
                ))}
        </>
    );
}

function StaffMeals({
    studentId,
    year,
    month,
}: {
    studentId: string;
    year: number;
    month: number;
}) {
    const student = useResource<Student>('/api/students/' + studentId);
    if (student.loading) return <p role="status">Loading student…</p>;
    if (student.error) return <RequestError error={student.error} retry={student.reload} />;
    return (
        <>
            <h2 className="application-section">
                {student.data!.firstName} {student.data!.lastName} · {student.data!.studentNumber}
            </h2>
            <MealRecords studentId={studentId} year={year} month={month} />
        </>
    );
}

function MealRecords({
    studentId,
    year,
    month,
}: {
    studentId?: string;
    year: number;
    month: number;
}) {
    const query = `year=${year}&month=${month}` + (studentId ? '&studentId=' + studentId : '');
    const entitlements = useResource<Entitlement[]>(
        (studentId ? '/api/meal-entitlements?' : '/api/meals/me/entitlements?') + query,
    );
    const [editing, setEditing] = useState<Entitlement | 'new' | null>(null);
    const [transaction, setTransaction] = useState<{ item: Entitlement; purchase: boolean } | null>(
        null,
    );
    const [version, setVersion] = useState(0);
    const [success, setSuccess] = useState('');
    const now = new Date();
    const currentMonth = year === now.getUTCFullYear() && month === now.getUTCMonth() + 1;
    const past =
        year < now.getUTCFullYear() ||
        (year === now.getUTCFullYear() && month < now.getUTCMonth() + 1);
    function pending(item: Entitlement, purchase: boolean) {
        try {
            return !!sessionStorage.getItem(
                `student-center.meal-request.${getSession()?.user.id}.${item.id}.${purchase ? 'purchase' : 'consume'}`,
            );
        } catch {
            return false;
        }
    }
    function saved() {
        setEditing(null);
        setTransaction(null);
        entitlements.reload();
        setVersion((value) => value + 1);
        setSuccess('Meal record saved.');
    }
    return (
        <>
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            <section className="panel application-section">
                <div className="heading-row">
                    <h2>Meal entitlements</h2>
                    <button
                        className="secondary"
                        disabled={!!editing || !!transaction || entitlements.loading}
                        onClick={() => {
                            entitlements.reload();
                            setVersion((value) => value + 1);
                        }}
                    >
                        Refresh meal records
                    </button>
                </div>
                {entitlements.loading ? (
                    <p role="status">Loading entitlements…</p>
                ) : entitlements.error ? (
                    <RequestError error={entitlements.error} retry={entitlements.reload} />
                ) : (
                    <>
                        {!entitlements.data?.length && (
                            <p>No meal entitlements found for this month.</p>
                        )}
                        {entitlements.data?.map((item) => (
                            <article className="staff-document" key={item.id}>
                                <h3>{mealTypes[item.mealType - 1]}</h3>
                                <StatusBadge
                                    status={
                                        ['', 'ACTIVE', 'SUSPENDED', 'EXPIRED'][item.status] ||
                                        'UNKNOWN'
                                    }
                                />
                                <p>Academic year: {item.academicYear}</p>
                                <dl className="details-grid">
                                    <div>
                                        <dt>Total allowance</dt>
                                        <dd>{item.allowedQuantity}</dd>
                                    </div>
                                    <div>
                                        <dt>Consumed</dt>
                                        <dd>{item.consumedQuantity}</dd>
                                    </div>
                                    <div>
                                        <dt>Remaining</dt>
                                        <dd>{item.remainingQuantity}</dd>
                                    </div>
                                </dl>
                                {studentId && (
                                    <div className="button-row">
                                        <button
                                            className="secondary"
                                            disabled={
                                                !!editing ||
                                                !!transaction ||
                                                item.status === 3 ||
                                                past
                                            }
                                            onClick={() => setEditing(item)}
                                        >
                                            Edit {mealTypes[item.mealType - 1].toLowerCase()}{' '}
                                            entitlement
                                        </button>
                                        <button
                                            className="primary"
                                            disabled={
                                                !!editing ||
                                                !!transaction ||
                                                (!pending(item, true) &&
                                                    (item.status !== 1 || past))
                                            }
                                            onClick={() => setTransaction({ item, purchase: true })}
                                        >
                                            {pending(item, true)
                                                ? 'Resume pending purchase'
                                                : 'Record paid purchase'}
                                        </button>
                                        <button
                                            className="secondary"
                                            disabled={
                                                !!editing ||
                                                !!transaction ||
                                                (!pending(item, false) &&
                                                    (item.status !== 1 ||
                                                        !currentMonth ||
                                                        item.remainingQuantity < 1))
                                            }
                                            onClick={() =>
                                                setTransaction({ item, purchase: false })
                                            }
                                        >
                                            {pending(item, false)
                                                ? 'Resume pending consumption'
                                                : 'Record consumption'}
                                        </button>
                                    </div>
                                )}
                            </article>
                        ))}
                        {studentId && (
                            <>
                                <button
                                    className="secondary"
                                    disabled={!!editing || !!transaction || past}
                                    onClick={() => setEditing('new')}
                                >
                                    Create entitlement
                                </button>
                                <p className="muted">
                                    Expired entitlements cannot be changed. Consumption is available
                                    only for the current UTC month and remaining meals.
                                </p>
                            </>
                        )}
                    </>
                )}
            </section>
            {editing && studentId && (
                <EntitlementForm
                    key={editing === 'new' ? 'new' : editing.id}
                    studentId={studentId}
                    year={year}
                    month={month}
                    item={editing === 'new' ? undefined : editing}
                    saved={saved}
                    cancel={() => setEditing(null)}
                />
            )}
            {transaction && (
                <MealTransaction
                    key={transaction.item.id + String(transaction.purchase)}
                    item={transaction.item}
                    purchase={transaction.purchase}
                    saved={saved}
                    cancel={() => setTransaction(null)}
                />
            )}
            <MealHistory
                key={'p' + version}
                purchases
                path={(studentId ? '/api/meal-purchases?' : '/api/meals/me/purchases?') + query}
            />
            <MealHistory
                key={'c' + version}
                purchases={false}
                path={
                    (studentId ? '/api/meal-consumptions?' : '/api/meals/me/consumptions?') + query
                }
            />
        </>
    );
}
