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
            <p className="muted">Choose an active staff account from the list.</p>
            <details>
                <summary>Filter staff accounts (optional)</summary>
                <label>
                    Username
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
            </details>
            {accounts.loading ? (
                <p role="status">Loading staff accounts…</p>
            ) : accounts.error ? (
                <RequestError error={accounts.error} retry={accounts.reload} />
            ) : (
                <>
                    {!accounts.data?.length && <p>No active staff accounts found.</p>}
                    {!!accounts.data?.length && (
                        <label>
                            Staff account
                            <select
                                defaultValue=""
                                onChange={(event) => {
                                    const account = accounts.data?.find(
                                        (item) => item.id === event.target.value,
                                    );
                                    if (account) select(account.id, account.username);
                                }}
                            >
                                <option value="" disabled>
                                    Select an employee
                                </option>
                                {accounts.data.map((account) => (
                                    <option key={account.id} value={account.id}>
                                        {account.username}
                                    </option>
                                ))}
                            </select>
                        </label>
                    )}
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
