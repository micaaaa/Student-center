import { useState } from 'react';
import { useResource } from '../hooks/useResource';
import { RequestError } from './ApplicationUi';

interface StaffAccount {
    id: string;
    username: string;
    email: string;
}
export function StaffAccountPicker({ select }: { select: (id: string, username: string) => void }) {
    const [input, setInput] = useState('');
    const [search, setSearch] = useState('');
    const [page, setPage] = useState(1);
    const accounts = useResource<StaffAccount[]>(
        `/api/staff-directory?search=${encodeURIComponent(search)}&page=${page}`,
    );
    return (
        <section aria-label="Find staff account">
            <label>
                Username or email
                <input
                    value={input}
                    maxLength={150}
                    onChange={(event) => setInput(event.target.value)}
                    onKeyDown={(event) => {
                        if (event.key === 'Enter') {
                            event.preventDefault();
                            setSearch(input.trim());
                            setPage(1);
                        }
                    }}
                />
            </label>
            <button
                type="button"
                className="secondary"
                onClick={() => {
                    setSearch(input.trim());
                    setPage(1);
                }}
            >
                Search staff
            </button>
            {accounts.loading ? (
                <p role="status">Loading staff accounts…</p>
            ) : accounts.error ? (
                <RequestError error={accounts.error} retry={accounts.reload} />
            ) : (
                <>
                    {!accounts.data?.length && <p>No active staff accounts found.</p>}
                    {accounts.data?.map((account) => (
                        <div className="student-result" key={account.id}>
                            <div>
                                <strong>{account.username}</strong>
                                <p>{account.email}</p>
                            </div>
                            <button
                                type="button"
                                className="secondary"
                                onClick={() => select(account.id, account.username)}
                            >
                                Select {account.username}
                            </button>
                        </div>
                    ))}
                    <div className="button-row application-section">
                        <button
                            type="button"
                            className="secondary"
                            disabled={page === 1}
                            onClick={() => setPage(page - 1)}
                        >
                            Previous staff
                        </button>
                        <span>Page {page}</span>
                        <button
                            type="button"
                            className="secondary"
                            disabled={accounts.data?.length !== 20 || page >= 100000}
                            onClick={() => setPage(page + 1)}
                        >
                            Next staff
                        </button>
                    </div>
                </>
            )}
        </section>
    );
}
