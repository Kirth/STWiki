// Collaborative editing client using simple block-based CRDT
class CollabClient {
    constructor(pageId, clientId, userName) {
        this.pageId = pageId;
        this.clientId = clientId;
        this.userName = userName;
        this.connection = null;
        this.blocks = [];
        this.version = 0;
        this.isConnected = false;
        
        // Simple event emitter
        this.events = {};
        
        this.initSignalR();
    }

    async initSignalR() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/collab")
                .withAutomaticReconnect()
                .build();

            this.connection.on("Init", (initPayload) => {
                console.log("Received init payload:", initPayload);
                console.log("Checkpoint type:", typeof initPayload.checkpoint, "Value:", initPayload.checkpoint);
                console.log("Updates type:", typeof initPayload.updates, "Length:", initPayload.updates?.length);
                
                this.version = initPayload.version;
                
                if (initPayload.checkpoint) {
                    console.log("Applying checkpoint...");
                    this.applyCheckpoint(initPayload.checkpoint);
                }
                
                if (initPayload.updates) {
                    console.log("Applying", initPayload.updates.length, "updates...");
                    initPayload.updates.forEach((update, i) => {
                        console.log(`Update ${i} type:`, typeof update, "Value:", update);
                        this.applyUpdate(update);
                    });
                }
                
                this.emit('initialized');
            });

            this.connection.on("Update", (updateData, seq) => {
                console.log("Received update:", seq);
                this.applyUpdate(updateData);
                this.version = Math.max(this.version, seq);
                this.emit('update');
            });

            this.connection.on("Presence", (presenceData) => {
                console.log("Received presence:", presenceData);
                this.emit('presence', presenceData);
            });

            this.connection.on("Error", (message) => {
                console.error("Collaboration error:", message);
                this.emit('error', message);
            });

            this.connection.on("TestResponse", (message) => {
                console.log("✅ Received test response:", message);
            });

            this.connection.onreconnected(() => {
                console.log("Reconnected to collaboration hub");
                this.isConnected = true;
                this.emit('connected');
            });

            this.connection.onreconnecting(() => {
                console.log("Reconnecting to collaboration hub...");
                this.isConnected = false;
                this.emit('reconnecting');
            });

            this.connection.onclose(() => {
                console.log("Disconnected from collaboration hub");
                this.isConnected = false;
                this.emit('disconnected');
            });

            await this.connection.start();
            this.isConnected = true;
            console.log("Connected to collaboration hub");
            
            // Initialize session
            await this.connection.invoke("Init", this.pageId, this.getVectorClock(), this.clientId);
            
        } catch (error) {
            console.error("Failed to initialize SignalR:", error);
            this.emit('error', error.message);
        }
    }

    // Simple event system
    on(event, handler) {
        if (!this.events[event]) this.events[event] = [];
        this.events[event].push(handler);
    }

    emit(event, data) {
        if (this.events[event]) {
            this.events[event].forEach(handler => handler(data));
        }
    }

    // Simple vector clock for now
    getVectorClock() {
        return JSON.stringify({ [this.clientId]: this.version });
    }

    // Apply checkpoint data
    applyCheckpoint(checkpointBytes) {
        try {
            const checkpointData = this.bytesToBlocks(checkpointBytes);
            this.blocks = checkpointData;
        } catch (error) {
            console.error("Failed to apply checkpoint:", error);
        }
    }

    // Apply individual update
    applyUpdate(updateData) {
        try {
            const update = this.parseUpdateData(updateData);
            console.log('📥 Applying update:', update);
            
            // Handle different update types
            if (update.type === 'content_update') {
                // Simple content update - emit to listeners
                this.emit('content_change', update.content);
            } else {
                // Block-based update
                this.applyBlockUpdate(update);
            }
        } catch (error) {
            console.error("Failed to apply update:", error);
        }
    }

    // Convert bytes to blocks (simplified)
    bytesToBlocks(bytes) {
        try {
            // Handle different data formats
            let text;
            if (typeof bytes === 'string') {
                text = bytes;
            } else if (bytes instanceof ArrayBuffer || bytes instanceof Uint8Array) {
                text = new TextDecoder().decode(bytes);
            } else if (Array.isArray(bytes)) {
                // Convert byte array to Uint8Array first
                text = new TextDecoder().decode(new Uint8Array(bytes));
            } else {
                console.warn('Unexpected bytes format:', typeof bytes, bytes);
                return [];
            }
            
            return JSON.parse(text);
        } catch (error) {
            console.warn('Failed to parse bytes as JSON:', error);
            // Fallback: treat as single text block
            const fallbackText = typeof bytes === 'string' ? bytes : 
                                 Array.isArray(bytes) ? new TextDecoder().decode(new Uint8Array(bytes)) : 
                                 'Unable to decode content';
            return [{
                id: 'default',
                type: 'paragraph',
                text: fallbackText
            }];
        }
    }

    // Parse update data (now expects string directly)
    parseUpdateData(data) {
        try {
            if (typeof data === 'string') {
                return JSON.parse(data);
            } else if (typeof data === 'object') {
                // Already parsed object
                return data;
            } else {
                console.warn('Unexpected update data format:', typeof data, data);
                return { type: 'unknown', data: data };
            }
        } catch (error) {
            console.error('Failed to parse update data:', error);
            return { type: 'error', message: 'Failed to parse update' };
        }
    }

    // Legacy method for backward compatibility
    bytesToUpdate(bytes) {
        return this.parseUpdateData(bytes);
    }

    // Apply block update
    applyBlockUpdate(update) {
        switch (update.type) {
            case 'insert':
                this.insertBlock(update.blockId, update.block, update.position);
                break;
            case 'update':
                this.updateBlock(update.blockId, update.changes);
                break;
            case 'delete':
                this.deleteBlock(update.blockId);
                break;
        }
    }

    // Insert block at position
    insertBlock(blockId, block, position = -1) {
        if (position === -1) {
            this.blocks.push({ id: blockId, ...block });
        } else {
            this.blocks.splice(position, 0, { id: blockId, ...block });
        }
    }

    // Update existing block
    updateBlock(blockId, changes) {
        const block = this.blocks.find(b => b.id === blockId);
        if (block) {
            Object.assign(block, changes);
        }
    }

    // Delete block
    deleteBlock(blockId) {
        this.blocks = this.blocks.filter(b => b.id !== blockId);
    }

    // Simple test method
    async testPushMethod() {
        if (!this.isConnected) {
            console.warn("Not connected - test skipped");
            return;
        }

        try {
            console.log('🧪 Testing simple push method...');
            await this.connection.invoke("TestPush", "Hello from client");
            console.log('✅ Simple test succeeded');
            
            console.log('🧪 Now testing complex Push method...');
            await this.sendUpdate({
                type: 'test',
                message: 'Test update',
                timestamp: Date.now()
            });
        } catch (error) {
            console.error("❌ Test failed:", error);
        }
    }

    // Send update to server
    async sendUpdate(update) {
        if (!this.isConnected) {
            console.warn("Not connected - update queued");
            return;
        }

        try {
            console.log('📤 Preparing to send update:', {
                pageId: this.pageId,
                clientId: this.clientId,
                updateSize: JSON.stringify(update).length,
                connectionState: this.connection.state
            });
            
            const updateData = JSON.stringify(update);
            const vectorClock = this.getVectorClock();
            
            console.log('📤 Calling Push method with:', {
                pageId: this.pageId,
                updateDataLength: updateData.length,
                vectorClock: vectorClock,
                clientId: this.clientId
            });
            
            await this.connection.invoke("Push", this.pageId, updateData, vectorClock, this.clientId);
            console.log('✅ Push method call succeeded');
            this.version++;
        } catch (error) {
            console.error("❌ Failed to send update:", error);
            console.error("❌ Error details:", {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
        }
    }

    // Send presence information
    async sendPresence(cursorPosition, selection) {
        if (!this.isConnected) return;

        try {
            const presence = {
                clientId: this.clientId,
                userName: this.userName,
                cursor: cursorPosition,
                selection: selection,
                timestamp: Date.now()
            };
            
            await this.connection.invoke("Presence", this.pageId, JSON.stringify(presence));
        } catch (error) {
            console.error("Failed to send presence:", error);
        }
    }

    // Create text block
    createTextBlock(id, text, type = 'paragraph') {
        return {
            id: id,
            type: type,
            text: text
        };
    }

    // Send text update
    async updateText(blockId, newText) {
        const update = {
            type: 'update',
            blockId: blockId,
            changes: { text: newText },
            clientId: this.clientId,
            timestamp: Date.now()
        };

        this.updateBlock(blockId, { text: newText });
        await this.sendUpdate(update);
    }

    // Add new block
    async addBlock(type = 'paragraph', text = '', position = -1) {
        const blockId = this.generateBlockId();
        const block = this.createTextBlock(blockId, text, type);
        
        const update = {
            type: 'insert',
            blockId: blockId,
            block: { type: type, text: text },
            position: position,
            clientId: this.clientId,
            timestamp: Date.now()
        };

        this.insertBlock(blockId, { type: type, text: text }, position);
        await this.sendUpdate(update);
        
        return blockId;
    }

    // Remove block
    async removeBlock(blockId) {
        const update = {
            type: 'delete',
            blockId: blockId,
            clientId: this.clientId,
            timestamp: Date.now()
        };

        this.deleteBlock(blockId);
        await this.sendUpdate(update);
    }

    // Generate unique block ID
    generateBlockId() {
        return `block-${this.clientId}-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }

    // Commit changes to revision
    async commit(message) {
        if (!this.isConnected) {
            throw new Error("Not connected to collaboration server");
        }

        try {
            const revisionId = await this.connection.invoke("Commit", this.pageId, message);
            console.log("Committed to revision:", revisionId);
            return revisionId;
        } catch (error) {
            console.error("Failed to commit:", error);
            throw error;
        }
    }

    // Get current document state as markdown
    toMarkdown() {
        return this.blocks.map(block => {
            switch (block.type) {
                case 'heading':
                    return `## ${block.text}`;
                case 'paragraph':
                    return block.text;
                case 'code':
                    return `\`\`\`\n${block.text}\n\`\`\``;
                default:
                    return block.text;
            }
        }).join('\n\n');
    }

    // Load content from markdown
    fromMarkdown(markdown) {
        const lines = markdown.split('\n\n');
        this.blocks = lines.map((line, index) => {
            if (line.startsWith('## ')) {
                return this.createTextBlock(`heading-${index}`, line.substring(3), 'heading');
            } else if (line.startsWith('```') && line.endsWith('```')) {
                return this.createTextBlock(`code-${index}`, line.slice(3, -3).trim(), 'code');
            } else {
                return this.createTextBlock(`paragraph-${index}`, line, 'paragraph');
            }
        }).filter(block => block.text.length > 0);
    }

    // Cleanup
    async disconnect() {
        if (this.connection) {
            await this.connection.stop();
        }
    }
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = CollabClient;
} else {
    window.CollabClient = CollabClient;
}