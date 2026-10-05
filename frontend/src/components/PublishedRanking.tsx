import { RequestError } from './ApplicationUi';
import { dateTime } from '../lib/applications';
import { useOptionalResource } from '../lib/results';
import type { Ranking } from '../lib/results';

export function PublishedRanking({
    competitionId,
    final = false,
}: {
    competitionId: string;
    final?: boolean;
}) {
    const name = final ? 'Final' : 'Preliminary';
    const resource = useOptionalResource<Ranking>(
        `/api/competitions/${competitionId}/rankings/${final ? 'final' : 'preliminary'}`,
        [
            `${name} ranking was not found.`,
            `Published ${name.toLowerCase()} ranking was not found.`,
        ],
    );
    const ranking = resource.data;
    return (
        <section className="panel application-section">
            <h2>{name} ranking</h2>
            {resource.loading ? (
                <p role="status">Loading ranking…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : !ranking ? (
                <p className="muted">The {name.toLowerCase()} ranking has not been published.</p>
            ) : (
                <>
                    <p className="muted">
                        Published:{' '}
                        {ranking.publishedAtUtc
                            ? dateTime(ranking.publishedAtUtc)
                            : 'Not published'}
                    </p>
                    <p>
                        {ranking.tieRule === 'SharedPosition'
                            ? 'Candidates with equal points share a position.'
                            : 'Candidates with equal points are ordered by submission time.'}
                    </p>
                    {final && (
                        <p>
                            Available places: <strong>{ranking.availablePlaces}</strong>
                        </p>
                    )}
                    <p className="muted">
                        Application references protect candidate privacy. Your application is
                        highlighted.
                    </p>
                    {ranking.entries.length === 0 ? (
                        <p>No applications on this ranking.</p>
                    ) : (
                        <div
                            className="ranking-scroll"
                            role="region"
                            aria-label={`${name} ranking table`}
                            tabIndex={0}
                        >
                            <table className="ranking-table">
                                <caption>{name} ranking — published results</caption>
                                <thead>
                                    <tr>
                                        <th scope="col">Position</th>
                                        <th scope="col">Application reference</th>
                                        <th scope="col">Points</th>
                                        {final && <th scope="col">Accommodation eligibility</th>}
                                    </tr>
                                </thead>
                                <tbody>
                                    {ranking.entries.map((entry) => (
                                        <tr
                                            key={entry.applicationId}
                                            className={entry.isMine ? 'ranking-mine' : undefined}
                                        >
                                            <td>{entry.position}</td>
                                            <td className="record-reference">
                                                {entry.applicationId}
                                                {entry.isMine && (
                                                    <strong className="ranking-owner">
                                                        Your application
                                                    </strong>
                                                )}
                                            </td>
                                            <td>{entry.totalPoints}</td>
                                            {final && (
                                                <td>
                                                    {entry.eligible ? 'Eligible' : 'Not eligible'}
                                                </td>
                                            )}
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </>
            )}
        </section>
    );
}
