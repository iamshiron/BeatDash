import { SignInIcon } from '@phosphor-icons/react';
import { createFileRoute } from '@tanstack/react-router';
import { Button } from '@shiron/ui/components/ui/button';

export const Route = createFileRoute('/')({
    component: HomePage,
});

function HomePage() {
    return (
        <div className="flex flex-col items-center justify-center min-h-[60vh]">
            <div className="text-center mb-8">
                <h1 className="text-4xl font-bold mb-4">
                    <span className="text-primary">BeatDash</span>
                </h1>
                <p className="text-muted-foreground text-lg max-w-md">
                    Track your BeatSaber stats in a single place and get
                    detailed insights of habits.
                </p>
            </div>
        </div>
    );
}
