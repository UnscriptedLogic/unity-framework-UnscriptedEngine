mergeInto(LibraryManager.library, {
    UnscriptedSave_Request: function (receiverPointer, databasePointer, request, operationPointer, slotPointer, generation, payloadPointer) {
        var receiver = UTF8ToString(receiverPointer);
        var database = UTF8ToString(databasePointer);
        var operation = UTF8ToString(operationPointer);
        var slot = UTF8ToString(slotPointer);
        var payload = UTF8ToString(payloadPointer);
        var connection = null;
        var finished = false;
        function finish(status, value) {
            if (finished) return;
            finished = true;
            if (connection) connection.close();
            SendMessage(receiver, "OnStorageComplete", request + "|" + status + "|" + value);
        }
        try {
            var open = indexedDB.open(database, 1);
            open.onupgradeneeded = function () {
                if (!open.result.objectStoreNames.contains("slots")) open.result.createObjectStore("slots");
            };
            open.onerror = function () { finish("error", String(open.error)); };
            open.onblocked = function () { finish("error", "Database upgrade blocked by another tab."); };
            open.onsuccess = function () {
                connection = open.result;
                if (finished) { connection.close(); return; }
                try {
                    var transaction = connection.transaction("slots", operation === "read" ? "readonly" : "readwrite");
                    var store = transaction.objectStore("slots");
                    var result = "";
                    var key = slot + "." + generation;
                    transaction.oncomplete = function () { finish("ok", result); };
                    transaction.onabort = function () { finish("error", String(transaction.error || "Transaction aborted.")); };
                    transaction.onerror = function () { /* onabort reports transaction failure. */ };
                    if (operation === "read") {
                        var read = store.get(key);
                        read.onsuccess = function () { result = read.result === undefined ? "missing" : read.result; };
                    } else if (operation === "write") {
                        store.put(payload, key);
                    } else if (operation === "delete") {
                        store.delete(slot + ".0");
                        store.delete(slot + ".1");
                    } else {
                        transaction.abort();
                    }
                } catch (error) { finish("error", String(error)); }
            };
        } catch (error) { finish("error", String(error)); }
    }
});
