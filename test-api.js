const axios = require('axios');

async function testApi() {
    try {
        console.log("Testing /api/risk/incidents/by-action with action=BLOCK");
        const res = await axios.get('http://localhost:5034/api/risk/incidents/by-action', {
            params: {
                action: 'BLOCK',
                page: 1,
                pageSize: 25
            }
        });

        console.log("Status:", res.status);
        console.log("Keys in response.data:", Object.keys(res.data));
        console.log("totalCount:", res.data.totalCount);
        console.log("totalPages:", res.data.totalPages);
        console.log("items length:", res.data.items?.length);

        if (res.data.items && res.data.items.length > 0) {
            console.log("First item keys:", Object.keys(res.data.items[0]));
        }
    } catch (err) {
        console.error("Error:", err.message);
        if (err.response) {
            console.error("Response data:", err.response.data);
        }
    }
}

testApi();
