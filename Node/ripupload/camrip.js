var spawn = require('child_process').spawn;
var tmp = require('tmp');
var del = require('del');
var request = require('request');
var fs = require('fs');

var PushBullet = require('pushbullet');

require('dotenv').config();

class ripper{
    constructor(){
        this._code = process.env.CODE;
        this._pusher = new PushBullet(process.env.PUSHBULLET);

    }

    startRip(name){

        var tmpFile = tmp.fileSync();
        var vidFile = tmpFile.name + ".mp4";
        var args = [
            '-y',
            '-i', 'http://10.0.0.141:99/videostream.asf?user=admin&pwd=',
            '-t', '15',
            '-r', '3', 
            '-c:v', 'libx264',
            '-b', '50000', 
            '-pix_fmt', 'yuv420p', 
            '-f', 'mp4', 
            vidFile
        ]

        var ffmpeg = spawn('ffmpeg', args);
										
	    console.log('Spawning ffmpeg ' + args.join(' '));

        ffmpeg.stdout.on('data', (data) => {
            console.log(data.toString());
        });

        ffmpeg.stderr.on('data', (data) => {
            console.log(data.toString());
        });

        ffmpeg.on('error', (err)=>{
            console.log("Error");
            console.log(err);
            process.exit(1);
        })

	    ffmpeg.on('exit', (code)=>{
            console.log(code);
            console.log(vidFile);

            this._pusher.file({}, vidFile, 'Motion Sensed', (error, response)=> {
                console.log(`Push Result: ${JSON.stringify(response)}`)
                var dt = new Date();
                var ticks = ((dt.getTime() * 10000) + 621355968000000000);

                var func =
                `https://jordocore.azurewebsites.net/api/MovementUploader?code=${this._code}&SourceName=${name}&Ext=mp4&Ticks=${ticks}`;
                console.log(`Posting to ${func}`);
                fs.createReadStream(vidFile).pipe(request.post(func)).on('end', ()=>{
                    del(vidFile, {force:true});
                    del(tmpFile.name, {force:true});
                    process.exit(0);
                });
            });

            
            
        });
    }
}

var r = new ripper();
r.startRip("doorcam");