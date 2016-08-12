// @function
// @param {string} id - The id for the active transactions document.
// @param {string} transactionId - The id for the new transaction.
function addActiveTransaction(id, transactionId) {
	var collection = getContext().getCollection();
	var collectionLink = collection.getSelfLink();
	var response = getContext().getResponse();

	// Validate input
	if (!id) throw new Error("The active transactions document id is undefined or null.");
	if (!transactionId) throw new Error("The transactionId is undefined or null.");

	tryQueryAndUpdate();

	function tryQueryAndUpdate() {
		var query = {query: "select * from root r where r.id = @id", parameters: [{name: "@id", value: id}]};
		var requestOptions = {};
		var isAccepted = collection.queryDocuments(collectionLink, query, requestOptions, function(err, documents, responseOptions) {
			if (err) throw err;
			if (documents.length > 0) {
				tryUpdate(documents[0])
			} else {
				throw new Error("Document not found.");
			}
		});

		// If we hit execution bounds - throw an exception.
        // This is highly unlikely given that this is a query by id; but is included to serve as an example for larger queries.
		if(!isAccepted) {
			throw new Error("The stored procedure timed out.");
		}
	}

	// Updates the active transactions document according to the id passed in to the sproc. 
	function tryUpdate(doc) {
		doc.Transactions.push(transactionId);
		requestOptions = {};
		var isAccepted = collection.replaceDocument(doc._self, doc, requestOptions, function (err, updatedDocument, responseOptions) {
            if (err) throw err;

            // If we have successfully updated the document - return it in the response body.
            response.setBody(updatedDocument);
        });

        if (!isAccepted) {
        	throw new Error("The stored procedure timed out.");
        }
	}
}