import { Link } from 'react-router';
import type { StaffRanking } from '../lib/staffRankings';
import { dateTime } from '../lib/applications';
import { StatusBadge } from './ApplicationUi';

export function StaffRankingTable({
    ranking,
    final = false,
}: {
    ranking: StaffRanking;
    final?: boolean;
}) {
    return (
        <>
            <StatusBadge status={ranking.status} />
            <p className="muted">
                {ranking.publishedAtUtc
                    ? 'Published: ' + dateTime(ranking.publishedAtUtc)
                    : 'Draft — not visible to students.'}
            </p>
            <p>
                {ranking.tieRule === 'SharedPosition'
                    ? 'Candidates with equal points share a position.'
                    : 'Candidates with equal points are ordered by submission time.'}
            </p>
            {final && <p>Available places in this ranking: {ranking.availablePlaces}</p>}
            <div
                className="ranking-scroll"
                role="region"
                aria-label={final ? 'Final ranking' : 'Preliminary ranking'}
                tabIndex={0}
            >
                <table className="ranking-table">
                    <caption>{final ? 'Final' : 'Preliminary'} ranking entries</caption>
                    <thead>
                        <tr>
                            <th scope="col">Position</th>
                            <th scope="col">Application</th>
                            <th scope="col">Points</th>
                            {final && <th scope="col">Eligibility</th>}
                        </tr>
                    </thead>
                    <tbody>
                        {ranking.entries.map((entry) => (
                            <tr key={entry.applicationId}>
                                <td>{entry.position}</td>
                                <td className="record-reference">
                                    <Link to={'/staff/applications/' + entry.applicationId}>
                                        {entry.applicationId}
                                    </Link>
                                </td>
                                <td>{entry.totalPoints}</td>
                                {final && <td>{entry.eligible ? 'Eligible' : 'Not eligible'}</td>}
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            {!ranking.entries.length && <p>No applications on this ranking.</p>}
        </>
    );
}
