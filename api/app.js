const express = require('express')
const cors = require('cors')
const fromEntries = require('fromentries')

const app = express()
app.use(express.json())
app.use(cors())

const port = 2021

let data = {}
let idMap = new Map()

app.post('/', (req, res) => {
    data = req.body
//  console.log(data)
    res.send('Proximity data posted.')
})

app.get('/map', (req, res) => {
    res.send(fromEntries(idMap))
})

app.get('/raw', (req, res) => {
    res.send(data)
})

app.get('/:name', (req, res) => {
    const name = req.params.name
    if (!data.hasOwnProperty(name)) {
        res.status(404).send('Player not found.')
    } else {
        res.send(data[name])
    }
})

app.post('/:name', (req, res) => {
    const name = req.params.name
    idMap.set(name, req.body.Id)
    res.send('Player ID mapped.')
})

app.delete('/:name', (req, res) => {
    const name = req.params.name
    idMap.delete(name)
    res.send('Player ID unmapped.')
})

app.listen(port, () => {
    console.log(`API listening at http://localhost:${port}`)
})
