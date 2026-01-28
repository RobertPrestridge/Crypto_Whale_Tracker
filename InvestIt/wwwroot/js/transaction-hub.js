// SignalR Transaction Notification Hub Client

class TransactionHub {
    constructor() {
        this.connection = null;
        this.isConnected = false;
        this.newTransactionCount = 0;
    }

    async start() {
        // Show connecting status before attempting connection
        this.showConnectingStatus();

        try {
            // Create SignalR connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/transactions")
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        if (retryContext.elapsedMilliseconds < 60000) {
                            return Math.random() * 10000;
                        } else {
                            return null;
                        }
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Set up event handlers
            this.setupEventHandlers();

            // Start connection with timeout
            const connectionPromise = this.connection.start();
            const timeoutPromise = new Promise((_, reject) =>
                setTimeout(() => reject(new Error('Connection timeout')), 10000)
            );

            await Promise.race([connectionPromise, timeoutPromise]);
            this.isConnected = true;
            console.log("SignalR Connected");
            this.showConnectionStatus(true);

        } catch (err) {
            console.error("SignalR Connection Error:", err);
            this.showConnectionStatus(false);

            // Retry connection after 5 seconds
            setTimeout(() => this.start(), 5000);
        }
    }

    showConnectingStatus() {
        const statusIndicator = document.getElementById('signalr-status');
        const liveIndicator = document.getElementById('live-indicator');

        if (statusIndicator) {
            statusIndicator.className = 'badge status-connecting';
            statusIndicator.textContent = 'Connecting...';
        }

        if (liveIndicator) {
            liveIndicator.style.display = 'none';
        }
    }

    setupEventHandlers() {
        // Handle incoming transaction notifications
        this.connection.on("ReceiveTransaction", (transaction) => {
            console.log("New transaction received:", transaction);
            this.handleTransactionNotification(transaction);
        });

        // Handle wallet-specific notifications
        this.connection.on("ReceiveWalletTransaction", (transaction) => {
            console.log("Wallet transaction received:", transaction);
            this.handleTransactionNotification(transaction);
        });

        // Handle pong response for connection test
        this.connection.on("Pong", (response) => {
            console.log("Pong received:", response);
        });

        // Handle reconnection
        this.connection.onreconnecting((error) => {
            console.log("SignalR reconnecting:", error);
            this.showReconnectingStatus();
        });

        this.connection.onreconnected((connectionId) => {
            console.log("SignalR reconnected:", connectionId);
            this.showConnectionStatus(true);
        });

        this.connection.onclose((error) => {
            console.log("SignalR connection closed:", error);
            this.isConnected = false;
            this.showConnectionStatus(false);

            // Attempt to reconnect
            setTimeout(() => this.start(), 5000);
        });
    }

    async ping() {
        if (this.isConnected) {
            console.log("Sending ping to server...");
            await this.connection.invoke("Ping");
        } else {
            console.log("Cannot ping - not connected");
        }
    }

    async sendTestNotification() {
        if (this.isConnected) {
            console.log("Requesting test notification from server...");
            await this.connection.invoke("SendTestNotification");
        } else {
            console.log("Cannot send test - not connected");
        }
    }

    handleTransactionNotification(transaction) {
        // Increment transaction counter
        this.newTransactionCount++;
        this.updateTransactionCounter();

        // Show toast notification
        this.showToast(transaction);

        // Update transactions table if it exists
        this.updateTransactionsTable(transaction);

        // Add flash effect to entire page
        this.flashNotification();

        // Trigger custom event for other parts of the app
        const event = new CustomEvent('transactionReceived', { detail: transaction });
        document.dispatchEvent(event);
    }

    updateTransactionCounter() {
        const counterElement = document.getElementById('new-tx-count');
        if (counterElement) {
            counterElement.textContent = this.newTransactionCount;
            counterElement.parentElement.classList.add('notification-badge');

            // Flash the counter
            counterElement.parentElement.style.animation = 'scaleIn 0.3s ease-out';
        }
    }

    flashNotification() {
        // Flash the entire transactions card
        const transactionsCard = document.querySelector('.card');
        if (transactionsCard) {
            transactionsCard.classList.add('transaction-flash');
            setTimeout(() => {
                transactionsCard.classList.remove('transaction-flash');
            }, 1000);
        }
    }

    resetTransactionCounter() {
        this.newTransactionCount = 0;
        const counterElement = document.getElementById('new-tx-count');
        if (counterElement) {
            counterElement.textContent = '0';
        }
    }

    showToast(transaction) {
        const toastContainer = document.getElementById('toast-container');
        if (!toastContainer) return;

        // Check if this is a whale transaction
        const isWhale = transaction.message &&
                       (transaction.message.toLowerCase().includes('whale') ||
                        transaction.walletLabel && transaction.walletLabel.toLowerCase().includes('whale'));

        const typeColor = transaction.type === 'Buy' ? 'success' :
                         transaction.type === 'Sell' ? 'warning' : 'info';

        const typeIcon = transaction.type === 'Buy' ? '📥' :
                        transaction.type === 'Sell' ? '📤' : '🔄';

        const whaleIcon = isWhale ? '🐋 ' : '';
        const whaleClass = isWhale ? 'whale-notification' : '';

        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${typeColor} border-0 ${whaleClass}`;
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');

        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <strong>${whaleIcon}${typeIcon} ${transaction.type} Transaction</strong><br>
                    ${transaction.message}<br>
                    <small>${transaction.amount.toFixed(4)} ${transaction.tokenSymbol}</small>
                    ${isWhale ? '<br><small class="text-warning">⚠️ Large Transaction Alert</small>' : ''}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        `;

        toastContainer.appendChild(toast);

        // Whale transactions get longer display time and play sound
        const delay = isWhale ? 10000 : 5000;
        const bsToast = new bootstrap.Toast(toast, { delay: delay });
        bsToast.show();

        // Play alert sound for whale transactions (if available)
        if (isWhale) {
            this.playWhaleAlert();
        }

        // Remove toast element after it's hidden
        toast.addEventListener('hidden.bs.toast', () => {
            toast.remove();
        });
    }

    playWhaleAlert() {
        // Optional: Play a notification sound for whale transactions
        // You can uncomment this if you add a sound file
        // const audio = new Audio('/sounds/whale-alert.mp3');
        // audio.play().catch(e => console.log('Audio play failed:', e));
    }

    updateTransactionsTable(transaction) {
        const tableBody = document.getElementById('transactions-table-body');
        if (!tableBody) return;

        const row = document.createElement('tr');
        row.className = 'transaction-new';

        const typeClass = transaction.type === 'Buy' ? 'text-success' :
                         transaction.type === 'Sell' ? 'text-warning' : 'text-info';

        const typeIcon = transaction.type === 'Buy' ? '📥' :
                        transaction.type === 'Sell' ? '📤' : '🔄';

        // Check if whale transaction
        const isWhale = transaction.message &&
                       (transaction.message.toLowerCase().includes('whale') ||
                        transaction.walletLabel && transaction.walletLabel.toLowerCase().includes('whale'));

        if (isWhale) {
            row.classList.add('whale-transaction');
        }

        const whaleIcon = isWhale ? '<span class="whale-badge" title="Whale Transaction over $10,000">🐋</span>' : '';

        row.innerHTML = `
            <td>${whaleIcon}<code class="small">${this.truncateHash(transaction.txHash)}</code></td>
            <td><span class="badge bg-primary">${transaction.networkName || 'N/A'}</span></td>
            <td class="${typeClass}"><strong>${typeIcon} ${transaction.type}</strong></td>
            <td class="${isWhale ? 'fw-bold' : ''}"><strong>${transaction.amount.toFixed(4)}</strong></td>
            <td><span class="badge bg-secondary">${transaction.tokenSymbol}</span></td>
            <td><small>${this.formatTimestamp(transaction.timestamp)}</small></td>
        `;

        // Insert at the beginning of the table
        tableBody.insertBefore(row, tableBody.firstChild);

        // Play sound notification (if browser allows)
        this.playNotificationSound();

        // Remove new styling after animation
        setTimeout(() => {
            row.classList.remove('transaction-new');
            row.classList.add('transaction-flash');
            setTimeout(() => row.classList.remove('transaction-flash'), 1000);
        }, 500);

        // Remove old rows if table gets too long
        while (tableBody.children.length > 50) {
            tableBody.removeChild(tableBody.lastChild);
        }
    }

    playNotificationSound() {
        // Optional: Play a subtle notification sound
        // Note: Most browsers require user interaction before playing sounds
        // Sound disabled - can be enabled by adding an audio file
    }

    showConnectionStatus(isConnected) {
        const statusIndicator = document.getElementById('signalr-status');
        const liveIndicator = document.getElementById('live-indicator');

        if (!statusIndicator) return;

        if (isConnected) {
            statusIndicator.className = 'badge status-connected status-pulse';
            statusIndicator.textContent = 'Connected';

            // Show LIVE badge
            if (liveIndicator) {
                liveIndicator.style.display = 'inline-block';
            }
        } else {
            statusIndicator.className = 'badge status-disconnected';
            statusIndicator.textContent = 'Disconnected';

            // Hide LIVE badge
            if (liveIndicator) {
                liveIndicator.style.display = 'none';
            }
        }
    }

    showReconnectingStatus() {
        const statusIndicator = document.getElementById('signalr-status');
        const liveIndicator = document.getElementById('live-indicator');

        if (statusIndicator) {
            statusIndicator.className = 'badge status-connecting';
            statusIndicator.textContent = 'Reconnecting...';
        }

        if (liveIndicator) {
            liveIndicator.style.display = 'none';
        }
    }

    truncateHash(hash) {
        if (!hash) return 'N/A';
        return hash.length > 16 ? `${hash.substr(0, 8)}...${hash.substr(-6)}` : hash;
    }

    formatTimestamp(timestamp) {
        const date = new Date(timestamp);
        // Format in US Central time (12-hour format)
        return date.toLocaleString('en-US', {
            timeZone: 'America/Chicago',
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: true
        });
    }

    async subscribeToWallet(walletId) {
        if (this.isConnected) {
            await this.connection.invoke("SubscribeToWallet", walletId);
            console.log(`Subscribed to wallet ${walletId}`);
        }
    }

    async unsubscribeFromWallet(walletId) {
        if (this.isConnected) {
            await this.connection.invoke("UnsubscribeFromWallet", walletId);
            console.log(`Unsubscribed from wallet ${walletId}`);
        }
    }
}

// Initialize and start the hub when DOM is ready
// Use window to make it globally accessible for onclick handlers
window.transactionHub = null;
document.addEventListener('DOMContentLoaded', () => {
    window.transactionHub = new TransactionHub();
    window.transactionHub.start();

    // Reset transaction counter when user focuses on the page
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden && window.transactionHub) {
            setTimeout(() => window.transactionHub.resetTransactionCounter(), 2000);
        }
    });

    // Add click handler to transaction count badge to reset it
    const txCountBadge = document.getElementById('new-tx-count');
    if (txCountBadge && txCountBadge.parentElement) {
        txCountBadge.parentElement.addEventListener('click', () => {
            if (window.transactionHub) {
                window.transactionHub.resetTransactionCounter();
            }
        });
        txCountBadge.parentElement.style.cursor = 'pointer';
        txCountBadge.parentElement.title = 'Click to reset counter';
    }
});
