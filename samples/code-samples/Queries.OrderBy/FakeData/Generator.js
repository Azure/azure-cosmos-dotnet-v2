// Use chance.js
for (var i = 0; i < 1000; i++) {
    var hashtag = {
        text: chance.hashtag(),
        indices: [chance.natural({ max: 10 }), chance.natural({ min: 11, max: 30 })]
    };

    var statusMessage =
    {
        "status_id": chance.natural(),
        "text": chance.sentence() + " " + hashtag.text,
        "user":
        {
            "user_id": chance.natural(),
            "name": chance.name(),
            "screen_name": chance.twitter(),
            "created_at": chance.timestamp(),
            "followers_count": chance.natural({ min: 1, max: 100 }),
            "friends_count": chance.natural({ min: 11, max: 30 }),
            "favourites_count": chance.natural({ min: 11, max: 30 }),
        },
        "created_at": chance.timestamp(),
        "favorite_count": chance.natural({ max: 100 }),
        "retweet_count": chance.natural({ max: 1000 }),
        "entities": { "hashtags": [hashtag] },
        "in_reply_to_status_id": chance.bool({ likelihood: 90 }) ? null : chance.natural()
    };

    console.log(JSON.stringify(statusMessage));
}