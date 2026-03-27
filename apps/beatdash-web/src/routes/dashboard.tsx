import { createFileRoute, Link } from '@tanstack/react-router';
import { Card, CardContent } from '@shiron/ui/components/ui/card';
import { Button } from '@shiron/ui/components/ui/button';

export const Route = createFileRoute('/dashboard')({
    component: DashboardPage,
});

function DashboardPage() {
    return (
        <div>
            <h1 className="text-2xl font-bold mb-6">Dashboard</h1>
        </div>
    );
}
