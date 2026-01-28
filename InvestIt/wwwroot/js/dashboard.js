// Dashboard specific JavaScript

document.addEventListener('DOMContentLoaded', () => {
    // Listen for transaction events
    document.addEventListener('transactionReceived', (event) => {
        const transaction = event.detail;
        console.log('Dashboard received transaction:', transaction);

        // Update statistics if elements exist
        updateStats();
    });

    // Initial stats load
    updateStats();
});

function updateStats() {
    // This could make an AJAX call to get updated statistics
    // For now, it's a placeholder
    console.log('Updating dashboard statistics...');
}

// Add CSS for new row animation
const style = document.createElement('style');
style.textContent = `
    .table-row-new {
        animation: highlightRow 2s ease-in-out;
    }

    @keyframes highlightRow {
        0% {
            background-color: #fff3cd;
        }
        100% {
            background-color: transparent;
        }
    }
`;
document.head.appendChild(style);
