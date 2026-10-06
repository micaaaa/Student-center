import { useEffect, useState, type FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Student } from '../lib/types';
import { RequestError } from './ApplicationUi';

type StudentSummary = Pick<
    Student,
    'id' | 'firstName' | 'lastName' | 'studentNumber' | 'email' | 'status'
>;
interface StudentResults {
    items: StudentSummary[];
    totalCount: number;
    page: number;
    pageSize: number;
}

export function StudentSearch({ onSelect }: { onSelect?: (id: string) => void }) {
    const [params, setParams] = useSearchParams();
    const search = params.get('studentSearch') || '';
    const requestedPage = Number(params.get('studentPage') || '1');
    const page =
        Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 100000
            ? requestedPage
            : 1;
    const [input, setInput] = useState(search);
    useEffect(() => setInput(search), [search]);
    function updateSearch(value: string, nextPage: number) {
        setParams(
            (current) => {
                const next = new URLSearchParams(current);
                if (value) next.set('studentSearch', value);
                else next.delete('studentSearch');
                if (nextPage > 1) next.set('studentPage', String(nextPage));
                else next.delete('studentPage');
                return next;
            },
            { replace: true },
        );
    }
    const results = useResource<StudentResults>(
        `/api/students?search=${encodeURIComponent(search)}&page=${page}&pageSize=20`,
    );
    function submit(event: FormEvent) {
        event.preventDefault();
        updateSearch(input.trim(), 1);
    }
    return (
        <section className="panel application-section" aria-label="Student search">
            <form onSubmit={submit}>
                <label>
                    Name or student number
                    <input
                        type="search"
                        maxLength={150}
                        value={input}
                        onChange={(event) => setInput(event.target.value)}
                        placeholder="e.g. Milica or 2026/001"
                    />
                </label>
                <div className="button-row application-section">
                    <button className="primary">Search</button>
                    <button
                        className="secondary"
                        type="button"
                        onClick={() => {
                            setInput('');
                            updateSearch('', 1);
                        }}
                    >
                        Show all
                    </button>
                </div>
            </form>
            {results.loading ? (
                <p role="status">Loading students…</p>
            ) : results.error ? (
                <RequestError error={results.error} retry={results.reload} />
            ) : (
                results.data && (
                    <>
                        <p role="status">
                            {results.data.totalCount} students found
                            {search ? ` for “${search}”` : ''}.
                        </p>
                        {results.data.items.length === 0 ? (
                            <p>No students on this page. Try another name or student number.</p>
                        ) : (
                            <div className="student-results">
                                {results.data.items.map((student) => (
                                    <article className="student-result" key={student.id}>
                                        <div>
                                            <strong>
                                                {student.firstName} {student.lastName}
                                            </strong>
                                            <p>
                                                {student.studentNumber} · {student.email}
                                            </p>
                                        </div>
                                        {onSelect ? (
                                            <button
                                                className="secondary"
                                                onClick={() => onSelect(student.id)}
                                                aria-label={`Select ${student.firstName} ${student.lastName}, ${student.studentNumber}`}
                                            >
                                                Select student
                                            </button>
                                        ) : (
                                            <Link
                                                className="text-link"
                                                to={'/staff/students/' + student.id}
                                            >
                                                View student
                                            </Link>
                                        )}
                                    </article>
                                ))}
                            </div>
                        )}
                        <div className="button-row application-section">
                            <button
                                type="button"
                                className="secondary"
                                disabled={page === 1}
                                onClick={() => updateSearch(search, page - 1)}
                            >
                                Previous
                            </button>
                            <span>
                                Page {page} of{' '}
                                {Math.max(1, Math.ceil(results.data.totalCount / 20))}
                            </span>
                            <button
                                type="button"
                                className="secondary"
                                disabled={page * 20 >= results.data.totalCount}
                                onClick={() => updateSearch(search, page + 1)}
                            >
                                Next
                            </button>
                        </div>
                    </>
                )
            )}
        </section>
    );
}
