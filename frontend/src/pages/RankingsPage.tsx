import { useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Competition } from '../lib/applications';
import { RequestError } from '../components/ApplicationUi';
import { PublishedRanking } from '../components/PublishedRanking';

export function RankingsPage() {
    const { id } = useParams();
    const competition = useResource<Competition>('/api/competitions/' + id);
    if (competition.loading) return <p role="status">Loading competition…</p>;
    if (competition.error)
        return <RequestError error={competition.error} retry={competition.reload} />;
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">PUBLISHED RESULTS</span>
                <h1>{competition.data!.name}</h1>
            </div>
            <PublishedRanking key={id + '-preliminary'} competitionId={id!} />
            <PublishedRanking key={id + '-final'} competitionId={id!} final />
        </>
    );
}
