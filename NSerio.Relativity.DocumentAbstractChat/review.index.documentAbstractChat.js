(function (params) {
    let dacCard = {
        type: "button",
        id: "document-abstract-chat-card",
        title: "Open Document Abstract Chat",
        singleton: true,
        icon: {
            fileName: "review.documentAbstractChat.png",
        },
        loader: {
            iframe: {
                fileName: "review.documentAbstractChat.html"
            }
        },
        location: {
            layoutId: "viewercollection",
            paneId: "ri-viewer-left-dock",
            dockIndex: 100
        },
        defaultWidth: 20,
        minWidth: 20
    };

    return {
        id: "documentAbstractChat.extension",
        name: "Extension to enable Document Abstract Chat features",
        cards: [dacCard],
        lifecycle: {
            ready: function (api) {
                let card = api.cards.getCard(dacCard.id);
                if (!card){
                    card = api.cards.createCard(dacCard.id, {
                        layoutId: dacCard.location.layoutId,
                        paneId: dacCard.location.paneId,
                    }, api.viewer.mainCollection);

                    api.viewer.mainCollection.on("contentchanged", function (e) {
                        let artifactId = e.item.artifactId
                        if (!card.document || artifactId != card.document.artifactId) {
                            let name = e.item.label;
                            card.document = {
                                artifactId, name
                            }
                            if (card.loadFrame) {
                                card.loadFrame();
                            }
                        }
                    });
                }                
            }
        }
    };
}(params));